#!/usr/bin/env python3

"""Распределяет интеграционные тесты по шардам и собирает их длительности из TRX."""

import json
import math
import os
import statistics
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path
from xml.sax.saxutils import escape


PARAMETERIZED_CASE_SPLIT_THRESHOLD = 256
PROFILE_MATRIX_BATCH_RUNS = 25
TIMINGS_SCHEMA_VERSION = 1
MIN_RECORDED_SECONDS = 0.000001
TIMINGS_PATH = Path(__file__).with_name("integration_test_timings.json")


def parse_tests(lines):
    """Извлекает полные имена из вывода `dotnet test --list-tests`."""
    list_headers = {
        "The following Tests are available:",
        "Доступны следующие тесты:",
    }
    tests = []
    in_list = False
    for line in lines:
        stripped = line.strip()
        if stripped in list_headers:
            in_list = True
            continue
        if not in_list:
            continue
        if not stripped:
            continue
        if not line[:1].isspace():
            break
        tests.append(stripped)
    return tests


def split_test_name(test):
    """Возвращает имя класса, метода и полное имя метода NUnit."""
    name = test.split("(", 1)[0].strip()
    dot = name.rfind(".")
    fixture = name[:dot] if dot > 0 else ""
    method = name[dot + 1:] if dot > 0 else name
    full_method = ".".join(part for part in (fixture, method) if part)
    return fixture, method, full_method


def _positive_number(value):
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and math.isfinite(value)
        and value > 0
    )


def load_timings(path=TIMINGS_PATH):
    """Загружает конфигурацию секунд и отклоняет повреждённые данные."""
    try:
        with open(path, encoding="utf-8") as file:
            timings = json.load(file)
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"cannot read integration test timings from {path}: {error}") from error

    if not isinstance(timings, dict) or timings.get("schemaVersion") != TIMINGS_SCHEMA_VERSION:
        raise ValueError(
            f"integration test timings must use schemaVersion {TIMINGS_SCHEMA_VERSION}"
        )

    for field in ("generatedAtUtc", "discoveryCommit"):
        if not isinstance(timings.get(field), str) or not timings[field]:
            raise ValueError(f"integration test timings field {field} must be a non-empty string")
    if (
        not isinstance(timings.get("profileRuns"), int)
        or isinstance(timings["profileRuns"], bool)
        or timings["profileRuns"] <= 0
    ):
        raise ValueError("integration test timings field profileRuns must be a positive integer")
    source_run_ids = timings.get("sourceRunIds")
    if not isinstance(source_run_ids, list) or not source_run_ids or any(
        not isinstance(run_id, int) or isinstance(run_id, bool) or run_id <= 0
        for run_id in source_run_ids
    ):
        raise ValueError("integration test timings field sourceRunIds must be an array of run IDs")

    for field in ("defaultCaseSeconds", "defaultMethodSeconds"):
        if not _positive_number(timings.get(field)):
            raise ValueError(f"integration test timings field {field} must be a positive number")

    for field in ("methodCaseSeconds", "caseSeconds"):
        values = timings.get(field)
        if not isinstance(values, dict):
            raise ValueError(f"integration test timings field {field} must be an object")
        if any(not isinstance(name, str) or not _positive_number(seconds) for name, seconds in values.items()):
            raise ValueError(
                f"integration test timings field {field} must map names to positive seconds"
            )

    return timings


def extract_groups(tests, timings, total_shards):
    """Группирует методы, а тяжёлые параметризованные методы делит по кейсам."""
    method_cases = {}
    for test in tests:
        fixture, method, full_method = split_test_name(test)
        method_cases.setdefault((fixture, method, full_method), []).append(test)

    estimates = {}
    unmeasured_methods = set()
    total_seconds = 0.0
    for key, cases in method_cases.items():
        full_method = key[2]
        method_default = timings["methodCaseSeconds"].get(full_method)
        exact = timings["caseSeconds"]
        unmeasured_method = method_default is None and not any(test in exact for test in cases)
        if unmeasured_method:
            unmeasured_methods.add(key)

        if unmeasured_method:
            method_total = max(
                timings["defaultMethodSeconds"],
                len(cases) * timings["defaultCaseSeconds"],
            )
            case_estimates = [method_total / len(cases)] * len(cases)
        else:
            fallback = method_default or timings["defaultCaseSeconds"]
            case_estimates = [exact.get(test, fallback) for test in cases]

        estimates[key] = case_estimates
        total_seconds += sum(case_estimates)

    target_seconds = total_seconds / total_shards
    group_counts = {}
    group_seconds = {}
    for key, cases in method_cases.items():
        fixture, method, full_method = key
        case_estimates = estimates[key]
        split_cases = len(set(cases)) > 1 and (
            key in unmeasured_methods
            or len(cases) > PARAMETERIZED_CASE_SPLIT_THRESHOLD
            or sum(case_estimates) > target_seconds
        )

        if split_cases:
            for test, seconds in zip(cases, case_estimates):
                group = (fixture, method, test)
                group_counts[group] = group_counts.get(group, 0) + 1
                group_seconds[group] = group_seconds.get(group, 0.0) + seconds
            continue

        group = (fixture, method, None)
        group_counts[group] = len(cases)
        group_seconds[group] = sum(case_estimates)

    return group_counts, group_seconds


def quote_tsl(value):
    """Экранирует значение для NUnit Test Selection Language."""
    return value.replace("\\", "\\\\").replace("'", "\\'")


def build_filter(groups):
    """Строит точный NUnit.Where для методов и отдельных тест-кейсов."""
    if not groups:
        return ""

    expressions = []
    for fixture, method, test in sorted(
        groups,
        key=lambda group: (group[0], group[1], group[2] or ""),
    ):
        if test is not None:
            expressions.append(f"test=='{quote_tsl(test)}'")
            continue

        method_expr = f"method=='{quote_tsl(method)}'"
        if fixture:
            expressions.append(f"(class=='{quote_tsl(fixture)}'&&{method_expr})")
        else:
            expressions.append(method_expr)

    return "||".join(expressions)


def build_runsettings(filter_expr):
    """Строит runsettings с фильтром адаптера NUnit."""
    if not filter_expr:
        filter_expr = "method=='__no_tests_assigned__'"

    return f"""<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <NUnit>
    <DisplayName>FullName</DisplayName>
    <MapWarningTo>Failed</MapWarningTo>
    <Where>{escape(filter_expr)}</Where>
  </NUnit>
</RunSettings>
"""


def distribute_groups(group_counts, group_seconds, total):
    """Кладёт следующую самую долгую группу в самый быстрый шард."""
    shards = [[] for _ in range(total)]
    shard_seconds = [0.0] * total

    for group in sorted(
        group_counts,
        key=lambda item: (
            -group_seconds[item],
            item[0],
            item[1],
            item[2] or "",
        ),
    ):
        lightest = min(range(total), key=lambda shard: (shard_seconds[shard], shard))
        shards[lightest].append(group)
        shard_seconds[lightest] += group_seconds[group]

    return shards, shard_seconds


def build_profile_matrices(profile_runs, total_shards):
    """Строит две матрицы профилирования в пределах лимита GitHub Actions."""
    entries = [
        {"profile_run": profile_run, "shard": shard}
        for profile_run in range(1, profile_runs + 1)
        for shard in range(total_shards)
    ]
    split = PROFILE_MATRIX_BATCH_RUNS * total_shards
    return {"include": entries[:split]}, {"include": entries[split:]}


def _parse_trx_duration(value):
    """Преобразует формат TimeSpan из TRX в секунды."""
    hours, minutes, seconds = value.split(":")
    days = 0
    if "." in hours:
        days, hours = hours.split(".", 1)
    return int(days) * 86400 + int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def collect_trx_results(directory):
    """Собирает только успешно завершившиеся тесты из всех TRX в каталоге."""
    results = {}
    for path in Path(directory).rglob("*.trx"):
        try:
            root = ET.parse(path).getroot()
        except (OSError, ET.ParseError) as error:
            print(f"Warning: cannot parse {path}: {error}", file=sys.stderr)
            continue

        for element in root.iter():
            if not element.tag.endswith("UnitTestResult") or element.get("outcome") != "Passed":
                continue
            name = element.get("testName")
            duration = element.get("duration")
            if name and duration:
                try:
                    results[name] = _parse_trx_duration(duration)
                except ValueError as error:
                    print(
                        f"Warning: cannot parse duration for {name} in {path}: {error}",
                        file=sys.stderr,
                    )
                    continue
    return results


def _trimmed_mean(values):
    ordered = sorted(values)
    trim = len(ordered) // 10
    if trim:
        ordered = ordered[trim:-trim]
    return statistics.fmean(ordered)


def _percentile(values, fraction):
    ordered = sorted(values)
    position = (len(ordered) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)


def _round_seconds(seconds):
    return max(round(seconds, 6), MIN_RECORDED_SECONDS)


def build_timing_config(tests, observations, profile_runs, commit, source_run_ids):
    """Строит итоговую конфигурацию по наблюдениям отдельных запусков."""
    required_observations = math.ceil(profile_runs * 0.8)
    case_seconds = {
        test: _trimmed_mean(list(observations[test].values()))
        for test in sorted(set(tests))
        if len(observations.get(test, {})) >= required_observations
    }
    if not case_seconds:
        raise ValueError("no tests have enough successful timing observations")

    method_cases = {}
    for test in tests:
        method_cases.setdefault(split_test_name(test)[2], []).append(test)

    method_case_seconds = {}
    method_totals = []
    for method, cases in method_cases.items():
        measured = [case_seconds[test] for test in cases if test in case_seconds]
        if not measured:
            continue
        fallback = statistics.median(measured)
        method_case_seconds[method] = fallback
        method_totals.append(sum(case_seconds.get(test, fallback) for test in cases))

    rounded_cases = {
        name: _round_seconds(seconds)
        for name, seconds in case_seconds.items()
    }
    rounded_methods = {
        name: _round_seconds(seconds)
        for name, seconds in sorted(method_case_seconds.items())
    }
    config = {
        "schemaVersion": TIMINGS_SCHEMA_VERSION,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
        "discoveryCommit": commit,
        "sourceRunIds": source_run_ids,
        "profileRuns": profile_runs,
        "defaultCaseSeconds": _round_seconds(
            _percentile(list(case_seconds.values()), 0.75)
        ),
        "defaultMethodSeconds": _round_seconds(_percentile(method_totals, 0.75)),
        "methodCaseSeconds": rounded_methods,
        "caseSeconds": rounded_cases,
    }
    missing = sorted(set(tests) - case_seconds.keys())
    return config, missing


def load_observations(directory, tests):
    """Объединяет результаты разных шардов по тесту и номеру повтора."""
    test_set = set(tests)
    observations = {}
    for path in Path(directory).glob("*.json"):
        try:
            sample = json.loads(path.read_text(encoding="utf-8"))
            profile_run = sample["profileRun"]
            values = sample["caseSeconds"]
            if (
                not isinstance(profile_run, int)
                or isinstance(profile_run, bool)
                or profile_run <= 0
            ):
                raise ValueError("profileRun must be a positive integer")
            if not isinstance(values, dict):
                raise ValueError("caseSeconds must be an object")
        except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
            print(f"Warning: ignoring invalid sample {path}: {error}", file=sys.stderr)
            continue
        for test, seconds in values.items():
            if test in test_set and _positive_number(seconds):
                observations.setdefault(test, {}).setdefault(profile_run, seconds)
    return observations


def cmd_generate():
    if len(sys.argv) != 4:
        print(f"Usage: {sys.argv[0]} generate <total-shards> <output-dir>", file=sys.stderr)
        sys.exit(1)

    try:
        total = int(sys.argv[2])
    except ValueError:
        print("Error: total-shards must be a positive integer", file=sys.stderr)
        sys.exit(1)
    if total <= 0:
        print("Error: total-shards must be a positive integer", file=sys.stderr)
        sys.exit(1)

    tests = parse_tests(sys.stdin.read().splitlines())
    if not tests:
        print("Error: no tests discovered from input", file=sys.stderr)
        sys.exit(1)

    try:
        timings = load_timings()
    except ValueError as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)

    group_counts, group_seconds = extract_groups(tests, timings, total)
    print(
        f"Discovered {len(tests)} tests in {len(group_counts)} groups, "
        f"distributing across {total} shards",
        file=sys.stderr,
    )

    output_dir = sys.argv[3]
    os.makedirs(output_dir, exist_ok=True)
    shards, shard_seconds = distribute_groups(group_counts, group_seconds, total)

    for shard in range(total):
        my_groups = sorted(
            shards[shard],
            key=lambda group: (group[0], group[1], group[2] or ""),
        )
        path = os.path.join(output_dir, f"shard_{shard}.runsettings")
        with open(path, "w", encoding="utf-8") as file:
            file.write(build_runsettings(build_filter(my_groups)))
        print(
            f"  Shard {shard}: {len(my_groups)} groups, "
            f"{shard_seconds[shard]:.1f} estimated seconds "
            f"({sum(group_counts[group] for group in my_groups)} tests)",
            file=sys.stderr,
        )


def cmd_matrix():
    if len(sys.argv) != 6:
        print(
            f"Usage: {sys.argv[0]} matrix <profile-runs> <max-parallel> "
            "<total-shards> <github-output>",
            file=sys.stderr,
        )
        sys.exit(1)

    try:
        profile_runs = int(sys.argv[2])
        max_parallel = int(sys.argv[3])
        total_shards = int(sys.argv[4])
    except ValueError:
        print(
            "Error: profile-runs, max-parallel and total-shards must be integers",
            file=sys.stderr,
        )
        sys.exit(1)

    if profile_runs not in (10, 20, 30, 50):
        print("Error: profile-runs must be one of: 10, 20, 30, 50", file=sys.stderr)
        sys.exit(1)
    if not 1 <= max_parallel <= 20:
        print("Error: max-parallel must be between 1 and 20", file=sys.stderr)
        sys.exit(1)
    if total_shards <= 0:
        print("Error: total-shards must be a positive integer", file=sys.stderr)
        sys.exit(1)

    matrix_first, matrix_second = build_profile_matrices(profile_runs, total_shards)
    with open(sys.argv[5], "a", encoding="utf-8") as output:
        output.write(
            f"matrix_first={json.dumps(matrix_first, separators=(',', ':'))}\n"
        )
        output.write(
            f"matrix_second={json.dumps(matrix_second, separators=(',', ':'))}\n"
        )
        output.write(f"has_second={str(bool(matrix_second['include'])).lower()}\n")
        output.write(f"max_parallel={max_parallel}\n")


def cmd_collect():
    if len(sys.argv) != 5:
        print(
            f"Usage: {sys.argv[0]} collect <profile-run> <trx-dir> <output-json>",
            file=sys.stderr,
        )
        sys.exit(1)

    try:
        profile_run = int(sys.argv[2])
    except ValueError:
        print("Error: profile-run must be an integer", file=sys.stderr)
        sys.exit(1)

    output = Path(sys.argv[4])
    output.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "profileRun": profile_run,
        "caseSeconds": collect_trx_results(sys.argv[3]),
    }
    output.write_text(json.dumps(data, ensure_ascii=False, sort_keys=True), encoding="utf-8")
    print(f"Collected {len(data['caseSeconds'])} successful test durations", file=sys.stderr)


def cmd_aggregate():
    if len(sys.argv) != 8:
        print(
            f"Usage: {sys.argv[0]} aggregate <discovery-log> <samples-dir> "
            "<output-json> <profile-runs> <commit> <source-run-id>",
            file=sys.stderr,
        )
        sys.exit(1)

    try:
        profile_runs = int(sys.argv[5])
        source_run_id = int(sys.argv[7])
    except ValueError:
        print("Error: profile-runs and source-run-id must be integers", file=sys.stderr)
        sys.exit(1)

    discovery = Path(sys.argv[2]).read_text(encoding="utf-8", errors="replace")
    tests = parse_tests(discovery.splitlines())
    if not tests:
        print("Error: no tests discovered from discovery log", file=sys.stderr)
        sys.exit(1)
    observations = load_observations(sys.argv[3], tests)

    try:
        config, missing = build_timing_config(
            tests,
            observations,
            profile_runs,
            sys.argv[6],
            [source_run_id],
        )
    except ValueError as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)

    output = Path(sys.argv[4])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(config, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print("## Профиль времени интеграционных тестов")
    print()
    print(f"Измерено тест-кейсов: {len(config['caseSeconds'])} из {len(set(tests))}.")
    print(f"Полных повторов: {profile_runs}; минимум успешных наблюдений: {math.ceil(profile_runs * 0.8)}.")
    print()
    print("### Самые долгие тест-кейсы")
    print()
    print("| Тест | Среднее после отсечения |")
    print("| --- | ---: |")
    for test, seconds in sorted(
        config["caseSeconds"].items(),
        key=lambda item: item[1],
        reverse=True,
    )[:20]:
        escaped_test = test.replace("|", "\\|")
        print(f"| `{escaped_test}` | {seconds:.3f} с |")
    if missing:
        print()
        print("### Недостаточно успешных наблюдений")
        print()
        print(f"Таких тест-кейсов: {len(missing)}. Для них сработают значения метода или общие значения.")
        for test in missing[:20]:
            print(f"- `{test}`")


def cmd_read():
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} read <runsettings-file>", file=sys.stderr)
        sys.exit(1)

    path = sys.argv[2]
    if not os.path.exists(path):
        return
    root = ET.parse(path).getroot()
    where = root.findtext("./NUnit/Where", default="").strip()
    if where:
        print("Running filtered test groups from the generated shard.", file=sys.stderr)
        print(where)


def main():
    if len(sys.argv) < 2:
        print(
            f"Usage: {sys.argv[0]} <generate|matrix|collect|aggregate|read> ...",
            file=sys.stderr,
        )
        sys.exit(1)

    commands = {
        "generate": cmd_generate,
        "matrix": cmd_matrix,
        "collect": cmd_collect,
        "aggregate": cmd_aggregate,
        "read": cmd_read,
    }
    command = commands.get(sys.argv[1])
    if command is None:
        print(f"Unknown command: {sys.argv[1]}", file=sys.stderr)
        sys.exit(1)
    command()


if __name__ == "__main__":
    main()
