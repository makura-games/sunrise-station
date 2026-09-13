#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SHARDING_SCRIPT="$ROOT_DIR/Tools/_sunrise/ci/sharding/test_shard_filter.py"
RESULTS_DIR=/tmp/test-results
PROFILE_SHARD_COUNT=8
cd "$ROOT_DIR"

setup_root_submodules() {
    git submodule update --init --recursive
}

setup_engine_submodules() {
    git -C RobustToolbox submodule update --init --recursive
}

restore_integration() {
    dotnet restore Content.IntegrationTests/Content.IntegrationTests.csproj
}

build_integration() {
    dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj \
        --configuration DebugOpt --no-restore /m
}

discover_shards() {
    local shard_count="${1:?Не указано количество шардов}"
    dotnet test --list-tests --no-build --no-restore --configuration DebugOpt \
        Content.IntegrationTests/Content.IntegrationTests.csproj \
        -- NUnit.DisplayName=FullName 2>&1 \
        | python3 "$SHARDING_SCRIPT" generate "$shard_count" .integration-filters
}

discover_profile() {
    dotnet test --list-tests --no-build --no-restore --configuration DebugOpt \
        Content.IntegrationTests/Content.IntegrationTests.csproj \
        -- NUnit.DisplayName=FullName 2>&1 \
        | tee integration-test-discovery.log \
        | python3 "$SHARDING_SCRIPT" generate "$PROFILE_SHARD_COUNT" .integration-filters
}

prune_build() {
    local variant="${1:?Не указан вариант артефакта}"
    test "$PWD" = "$GITHUB_WORKSPACE"
    test -f bin/Content.Client/Content.Client.dll
    test -f bin/Content.Server/Content.Server.dll
    test -f bin/Content.IntegrationTests/Content.IntegrationTests.dll

    find . -path './.git' -prune -o -type d -name obj -prune -exec rm -rf -- {} +
    find . -path './.git' -prune -o -type d -name bin ! -path './bin' -prune -exec rm -rf -- {} +

    case "$variant" in
        test)
            test -f bin/Content.Tests/Content.Tests.dll
            find bin -mindepth 1 -maxdepth 1 -type d \
                ! -name Content.Tests \
                ! -name Content.IntegrationTests \
                -exec rm -rf -- {} +
            ;;
        profile)
            find bin -mindepth 1 -maxdepth 1 -type d \
                ! -name Content.IntegrationTests \
                -exec rm -rf -- {} +
            ;;
        *)
            echo "Неизвестный вариант артефакта: $variant" >&2
            return 1
            ;;
    esac

    # Пустые каталоги нужны ResourceManager как точки монтирования сборок.
    mkdir -p bin/Content.Client bin/Content.Server
}

archive_build() {
    local variant="${1:?Не указан вариант артефакта}"
    case "$variant" in
        test)
            mkdir -p /tmp/integration-build-output/Tools/_sunrise/ci
            cp Tools/_sunrise/ci/test_workflow.sh \
                /tmp/integration-build-output/Tools/_sunrise/ci/test_workflow.sh
            tar -I 'zstd -T0 -3' -cf /tmp/integration-build-output/integration-build.tar.zst \
                Resources RobustToolbox/Resources .integration-filters \
                Tools/_sunrise/ci \
                bin/Content.Client bin/Content.Server bin/Content.IntegrationTests

            mkdir -p /tmp/content-tests-build-output/Tools/_sunrise/ci
            cp Tools/_sunrise/ci/test_workflow.sh \
                /tmp/content-tests-build-output/Tools/_sunrise/ci/test_workflow.sh
            tar -I 'zstd -T0 -3' -cf /tmp/content-tests-build-output/content-tests-build.tar.zst \
                Tools/_sunrise/ci bin/Content.Tests
            ;;
        profile)
            mkdir -p /tmp/integration-profile-build/Tools/_sunrise/ci
            cp Tools/_sunrise/ci/test_workflow.sh \
                /tmp/integration-profile-build/Tools/_sunrise/ci/test_workflow.sh
            tar -I 'zstd -T0 -3' -cf /tmp/integration-profile-build/integration-profile-build.tar.zst \
                Resources RobustToolbox/Resources .integration-filters Tools/_sunrise/ci \
                bin/Content.Client bin/Content.Server bin/Content.IntegrationTests
            ;;
        *)
            echo "Неизвестный вариант артефакта: $variant" >&2
            return 1
            ;;
    esac
}

extract_build() {
    local archive="${1:?Не указан архив сборки}"
    test -f "$archive"
    tar --zstd -xf "$archive"
    rm -- "$archive"
}

run_content_tests() {
    mkdir -p "$RESULTS_DIR"
    dotnet test bin/Content.Tests/Content.Tests.dll \
        --logger "trx;LogFileName=results.trx" \
        --results-directory "$RESULTS_DIR" \
        -- NUnit.ConsoleOut=0 NUnit.WorkDirectory="$RESULTS_DIR"
}

show_test_results() {
    dotnet tool install -g dotnet-trx 2>/dev/null || true
    mkdir -p "$RESULTS_DIR"
    trx --path "$RESULTS_DIR" -o -v quiet || true
}

run_integration_shard() {
    : "${SHARD:?Не указан номер шарда}"
    [[ "$SHARD" =~ ^[0-9]+$ ]]
    local settings=".integration-filters/shard_${SHARD}.runsettings"
    mkdir -p "$RESULTS_DIR"
    timeout --signal=TERM --kill-after=2m 15m \
        dotnet test bin/Content.IntegrationTests/Content.IntegrationTests.dll \
        --settings "$settings" \
        --logger "trx;LogFileName=results.trx" \
        --logger "console;verbosity=normal" \
        --results-directory "$RESULTS_DIR" \
        --blame-hang --blame-hang-timeout 6min --blame-hang-dump-type mini \
        -- NUnit.ConsoleOut=0 NUnit.WorkDirectory="$RESULTS_DIR"
}

report_timeout() {
    : "${SHARD:?Не указан номер шарда}"
    : "${GITHUB_STEP_SUMMARY:?Не указан файл сводки GitHub Actions}"
    shopt -s nullglob
    local sequences=("$RESULTS_DIR"/*/Sequence*.xml)
    if ((${#sequences[@]} == 0)); then
        return
    fi

    {
        echo "## :warning: Test timeout detected (shard $SHARD)"
        echo '```xml'
        cat "${sequences[@]}"
        echo '```'
        if [[ -n "${SEQUENCE_URL:-}" ]]; then
            echo ":page_facing_up: [Download timeout sequence]($SEQUENCE_URL)"
        fi
        if [[ -n "${DUMP_URL:-}" ]]; then
            echo ":floppy_disk: [Download timeout dump]($DUMP_URL)"
        fi
    } >> "$GITHUB_STEP_SUMMARY"
}

verify_required_jobs() {
    echo "build=$BUILD_RESULT content-tests=$CONTENT_TESTS_RESULT integration-tests=$INTEGRATION_TESTS_RESULT"
    if [[ "$BUILD_RESULT" != success \
        || "$CONTENT_TESTS_RESULT" != success \
        || "$INTEGRATION_TESTS_RESULT" != success ]]; then
        echo "::error title=Required CI failed::At least one required job did not succeed."
        return 1
    fi
}

prepare_profile_matrix() {
    python3 "$SHARDING_SCRIPT" matrix \
        "$PROFILE_RUNS" "$MAX_PARALLEL_RUNNERS" "$PROFILE_SHARD_COUNT" "$GITHUB_OUTPUT"
}

run_profile_shard() {
    : "${SHARD:?Не указан номер шарда}"
    [[ "$SHARD" =~ ^[0-9]+$ ]]
    local settings=".integration-filters/shard_${SHARD}.runsettings"
    mkdir -p "$RESULTS_DIR"
    timeout --signal=TERM --kill-after=2m 15m \
        dotnet test bin/Content.IntegrationTests/Content.IntegrationTests.dll \
        --settings "$settings" \
        --logger "trx;LogFileName=results.trx" \
        --logger "console;verbosity=minimal" \
        --results-directory "$RESULTS_DIR" \
        -- NUnit.ConsoleOut=0 NUnit.WorkDirectory="$RESULTS_DIR"
}

collect_profile() {
    : "${SHARD:?Не указан номер шарда}"
    python3 "$SHARDING_SCRIPT" collect \
        "$PROFILE_RUN" "$RESULTS_DIR" \
        "/tmp/integration-timing-${PROFILE_RUN}-${SHARD}.json"
}

aggregate_profile() {
    python3 "$SHARDING_SCRIPT" aggregate \
        integration-test-discovery.log integration-timing-samples \
        Tools/_sunrise/ci/sharding/integration_test_timings.json "$PROFILE_RUNS" \
        "$GITHUB_SHA" "$GITHUB_RUN_ID" \
        | tee -a "$GITHUB_STEP_SUMMARY"
}

command="${1:-}"
shift || true
case "$command" in
    setup-root-submodules) setup_root_submodules "$@" ;;
    setup-engine-submodules) setup_engine_submodules "$@" ;;
    restore-integration) restore_integration "$@" ;;
    build-integration) build_integration "$@" ;;
    discover-shards) discover_shards "$@" ;;
    discover-profile) discover_profile "$@" ;;
    prune-build) prune_build "$@" ;;
    archive-build) archive_build "$@" ;;
    extract-build) extract_build "$@" ;;
    run-content-tests) run_content_tests "$@" ;;
    show-test-results) show_test_results "$@" ;;
    run-integration-shard) run_integration_shard "$@" ;;
    report-timeout) report_timeout "$@" ;;
    verify-required-jobs) verify_required_jobs "$@" ;;
    prepare-profile-matrix) prepare_profile_matrix "$@" ;;
    run-profile-shard) run_profile_shard "$@" ;;
    collect-profile) collect_profile "$@" ;;
    aggregate-profile) aggregate_profile "$@" ;;
    *)
        echo "Неизвестная команда test_workflow.sh: $command" >&2
        exit 1
        ;;
esac
