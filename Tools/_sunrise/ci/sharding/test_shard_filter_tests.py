import importlib.util
import io
import json
import tempfile
import unittest
from contextlib import redirect_stderr
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("test_shard_filter.py")
SPEC = importlib.util.spec_from_file_location("test_shard_filter", SCRIPT_PATH)
SHARD_FILTER = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(SHARD_FILTER)

TIMINGS = {
    "defaultCaseSeconds": 1.0,
    "defaultMethodSeconds": 2.0,
    "methodCaseSeconds": {},
    "caseSeconds": {},
}

VALID_TIMINGS = {
    "schemaVersion": 1,
    "generatedAtUtc": "2026-08-09T00:00:00Z",
    "discoveryCommit": "commit",
    "sourceRunIds": [123],
    "profileRuns": 1,
    **TIMINGS,
}


class TestShardFilterTests(unittest.TestCase):
    def test_rejects_incorrect_timing_schema(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "timings.json"
            path.write_text(
                json.dumps({**VALID_TIMINGS, "schemaVersion": 2}),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "schemaVersion"):
                SHARD_FILTER.load_timings(path)

    def test_rejects_non_positive_timing_seconds(self):
        for seconds in (0.0, -1.0):
            with self.subTest(seconds=seconds), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "timings.json"
                path.write_text(
                    json.dumps({**VALID_TIMINGS, "defaultCaseSeconds": seconds}),
                    encoding="utf-8",
                )

                with self.assertRaisesRegex(ValueError, "positive number"):
                    SHARD_FILTER.load_timings(path)

    def test_parses_localized_test_list_header(self):
        tests = SHARD_FILTER.parse_tests(
            [
                "Доступны следующие тесты:",
                "    Content.Tests.Fixture.Test",
            ]
        )

        self.assertEqual(tests, ["Content.Tests.Fixture.Test"])

    def test_groups_identically_named_methods_by_fixture(self):
        timings = {
            **TIMINGS,
            "methodCaseSeconds": {
                "Content.Tests.SecondFixture.Test": 1.0,
            },
        }
        groups, seconds = SHARD_FILTER.extract_groups(
            [
                "Content.Tests.FirstFixture.Test",
                "Content.Tests.SecondFixture.Test",
                "Content.Tests.SecondFixture.Test(1)",
            ],
            timings,
            1,
        )

        self.assertEqual(
            groups,
            {
                ("Content.Tests.FirstFixture", "Test", None): 1,
                ("Content.Tests.SecondFixture", "Test", None): 2,
            },
        )
        self.assertEqual(seconds["Content.Tests.FirstFixture", "Test", None], 2.0)

    def test_splits_unmeasured_parameterized_methods_into_individual_cases(self):
        tests = [
            "Content.Tests.Fixture.Test(1)",
            "Content.Tests.Fixture.Test(2)",
        ]

        groups, _ = SHARD_FILTER.extract_groups(tests, TIMINGS, 8)

        self.assertEqual(
            groups,
            {
                ("Content.Tests.Fixture", "Test", tests[0]): 1,
                ("Content.Tests.Fixture", "Test", tests[1]): 1,
            },
        )

    def test_splits_large_parameterized_methods_into_individual_cases(self):
        case_count = SHARD_FILTER.PARAMETERIZED_CASE_SPLIT_THRESHOLD + 1
        tests = [f"Content.Tests.Fixture.Test({index})" for index in range(case_count)]

        groups, _ = SHARD_FILTER.extract_groups(tests, TIMINGS, 1)

        self.assertEqual(len(groups), case_count)
        self.assertEqual(
            groups[("Content.Tests.Fixture", "Test", tests[0])],
            1,
        )

    def test_builds_exact_fixture_and_method_filter(self):
        expression = SHARD_FILTER.build_filter(
            [
                ("Content.Tests.FirstFixture", "Test", None),
                ("Content.Tests.SecondFixture", "Test", None),
            ]
        )

        self.assertEqual(
            expression,
            "(class=='Content.Tests.FirstFixture'&&method=='Test')||"
            "(class=='Content.Tests.SecondFixture'&&method=='Test')",
        )

    def test_builds_exact_parameterized_case_filter(self):
        expression = SHARD_FILTER.build_filter(
            [
                (
                    "Content.Tests.Fixture",
                    "Test",
                    "Content.Tests.Fixture.Test('value')",
                ),
            ]
        )

        self.assertEqual(
            expression,
            "test=='Content.Tests.Fixture.Test(\\'value\\')'",
        )

    def test_supports_method_only_discovery(self):
        groups, _ = SHARD_FILTER.extract_groups(
            ["Test", "ParameterizedTest(1)"],
            TIMINGS,
            1,
        )

        self.assertEqual(
            groups,
            {
                ("", "Test", None): 1,
                ("", "ParameterizedTest", None): 1,
            },
        )
        self.assertEqual(
            SHARD_FILTER.build_filter(groups),
            "method=='ParameterizedTest'||method=='Test'",
        )

    def test_balances_individual_cases_across_shards(self):
        groups = {
            ("Content.Tests.Fixture", "Test", f"Content.Tests.Fixture.Test({index})"): 1
            for index in range(4)
        }
        seconds = {group: 1.0 for group in groups}

        shards, loads = SHARD_FILTER.distribute_groups(groups, seconds, 2)

        self.assertEqual(loads, [2.0, 2.0])
        self.assertEqual([len(shard) for shard in shards], [2, 2])

    def test_builds_single_profile_matrix_batch(self):
        first, second = SHARD_FILTER.build_profile_matrices(20, 8)

        self.assertEqual(len(first["include"]), 160)
        self.assertEqual(second, {"include": []})
        self.assertEqual(
            {(entry["profile_run"], entry["shard"]) for entry in first["include"]},
            {(profile_run, shard) for profile_run in range(1, 21) for shard in range(8)},
        )

    def test_splits_large_profile_matrix_without_duplicates(self):
        first, second = SHARD_FILTER.build_profile_matrices(50, 8)
        entries = first["include"] + second["include"]
        pairs = [(entry["profile_run"], entry["shard"]) for entry in entries]

        self.assertEqual(len(first["include"]), 200)
        self.assertEqual(len(second["include"]), 200)
        self.assertEqual(len(pairs), len(set(pairs)))
        self.assertEqual(
            set(pairs),
            {(profile_run, shard) for profile_run in range(1, 51) for shard in range(8)},
        )

    def test_builds_runsettings_with_escaped_filter(self):
        settings = SHARD_FILTER.build_runsettings("class=='Fixture'&&method=='Test'")

        self.assertIn("<DisplayName>FullName</DisplayName>", settings)
        self.assertIn("<MapWarningTo>Failed</MapWarningTo>", settings)
        self.assertIn(
            "<Where>class=='Fixture'&amp;&amp;method=='Test'</Where>",
            settings,
        )

    def test_splits_parameterized_method_that_exceeds_target_seconds(self):
        tests = [
            "Content.Tests.Fixture.Slow(1)",
            "Content.Tests.Fixture.Slow(2)",
            "Content.Tests.Fixture.Fast",
        ]
        timings = {
            **TIMINGS,
            "caseSeconds": {
                tests[0]: 10.0,
                tests[1]: 10.0,
                tests[2]: 1.0,
            },
        }

        groups, seconds = SHARD_FILTER.extract_groups(tests, timings, 2)

        self.assertIn(("Content.Tests.Fixture", "Slow", tests[0]), groups)
        self.assertEqual(seconds["Content.Tests.Fixture", "Slow", tests[0]], 10.0)

    def test_parses_trx_duration(self):
        self.assertEqual(
            SHARD_FILTER._parse_trx_duration("00:01:02.5000000"),
            62.5,
        )

    def test_skips_invalid_trx_duration(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.trx"
            path.write_text(
                "<TestRun><Results>"
                '<UnitTestResult testName="Tests.Valid" outcome="Passed" '
                'duration="00:00:01.2500000" />'
                '<UnitTestResult testName="Tests.Invalid" outcome="Passed" '
                'duration="invalid" />'
                "</Results></TestRun>",
                encoding="utf-8",
            )

            results = SHARD_FILTER.collect_trx_results(directory)

        self.assertEqual(results, {"Tests.Valid": 1.25})

    def test_keeps_rounded_timing_seconds_positive(self):
        test = "Content.Tests.Fixture.Fast"
        config, _ = SHARD_FILTER.build_timing_config(
            [test],
            {test: {1: 0.0000001}},
            1,
            "commit",
            [123],
        )

        self.assertEqual(
            config["caseSeconds"][test],
            SHARD_FILTER.MIN_RECORDED_SECONDS,
        )
        self.assertGreater(config["defaultCaseSeconds"], 0)
        self.assertGreater(config["defaultMethodSeconds"], 0)

    def test_requires_eighty_percent_of_timing_observations(self):
        measured = "Content.Tests.Fixture.Measured"
        missing = "Content.Tests.Fixture.Missing"
        observations = {
            measured: {run: 1.0 for run in range(8)},
            missing: {run: 1.0 for run in range(7)},
        }

        config, missing_tests = SHARD_FILTER.build_timing_config(
            [measured, missing],
            observations,
            10,
            "commit",
            [123],
        )

        self.assertEqual(config["caseSeconds"], {measured: 1.0})
        self.assertEqual(missing_tests, [missing])

    def test_merges_shard_samples_from_the_same_profile_run(self):
        tests = ["Content.Tests.Fixture.First", "Content.Tests.Fixture.Second"]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "run-1-shard-0.json").write_text(
                json.dumps({"profileRun": 1, "caseSeconds": {tests[0]: 1.0}}),
                encoding="utf-8",
            )
            (path / "run-1-shard-1.json").write_text(
                json.dumps({"profileRun": 1, "caseSeconds": {tests[1]: 2.0}}),
                encoding="utf-8",
            )

            observations = SHARD_FILTER.load_observations(path, tests)

        self.assertEqual(observations, {tests[0]: {1: 1.0}, tests[1]: {1: 2.0}})

    def test_ignores_invalid_profile_samples(self):
        test = "Content.Tests.Fixture.Valid"
        invalid_samples = {
            "null-cases.json": {"profileRun": 1, "caseSeconds": None},
            "zero-run.json": {"profileRun": 0, "caseSeconds": {test: 1.0}},
            "boolean-run.json": {"profileRun": True, "caseSeconds": {test: 1.0}},
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "valid.json").write_text(
                json.dumps({"profileRun": 1, "caseSeconds": {test: 2.0}}),
                encoding="utf-8",
            )
            for filename, sample in invalid_samples.items():
                (path / filename).write_text(json.dumps(sample), encoding="utf-8")

            stderr = io.StringIO()
            with redirect_stderr(stderr):
                observations = SHARD_FILTER.load_observations(path, [test])

        self.assertEqual(observations, {test: {1: 2.0}})
        for filename in invalid_samples:
            self.assertIn(filename, stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
