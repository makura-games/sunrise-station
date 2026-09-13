import copy
import json
import os
import sys
import tomllib
import unittest
from pathlib import Path
from urllib.request import Request
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[3]
AUTO_DRAFT_DIR = REPO_ROOT / "Tools" / "_sunrise" / "auto_draft"
CI_DIR = REPO_ROOT / "Tools" / "_sunrise" / "ci"
sys.path.insert(0, str(AUTO_DRAFT_DIR))
sys.path.insert(0, str(CI_DIR))

from checklist import MARKER, build_checklist, sync_checklist
from check_packaging_paths import load_workflow_paths, packaging_needed
from github_api import GitHub, GitHubError, HTTPSRedirectHandler
from readiness import load_readiness, timestamp
from report import build_report, publish_report
from review_threads import AutoDraft, coderabbit_conversation_state, decide_draft_state, load_config


HEAD = "1234567890abcdef1234567890abcdef12345678"
HEAD_COMMITTED_AT = "2026-09-01T00:00:00Z"
PULL_REQUEST = {
    "number": 1,
    "headRefOid": HEAD,
    "baseRefName": "master",
    "createdAt": HEAD_COMMITTED_AT,
}
RABBIT = {
    "__typename": "StatusContext",
    "context": "CodeRabbit",
    "state": "SUCCESS",
    "description": "Review completed",
    "createdAt": "2026-09-01T00:00:00Z",
    "creator": {"__typename": "Bot", "login": "coderabbitai"},
    "isRequired": False,
}
CHECK = {
    "__typename": "CheckRun",
    "name": "Tests",
    "databaseId": 1,
    "status": "COMPLETED",
    "conclusion": "SUCCESS",
    "isRequired": True,
    "checkSuite": {
        "app": {"databaseId": 15368, "slug": "github-actions"},
        "workflowRun": {"workflow": {"databaseId": 1}},
    },
}
REQUIREMENT = {"context": "Tests", "integration_id": 15368}


def limited_comment(body="Review rate limited."):
    return {
        "user": {"type": "Bot", "login": "coderabbitai[bot]"},
        "updated_at": "2026-09-01T00:00:00Z",
        "body": body,
    }


class ReadinessGitHub:
    def __init__(self, *, checks=None, comments=None, requirements=None, classic=None,
                 pages=None, response_head=HEAD, workflows=None, runs=None, reviews=None,
                 statuses=None, previous_attempt=None, fail=None):
        self.checks = [CHECK, RABBIT] if checks is None else checks
        self.comments = comments or []
        self.requirements = [REQUIREMENT] if requirements is None else requirements
        self.classic = classic or []
        self.pages = pages
        self.response_head = response_head
        self.workflows = workflows or []
        self.runs = runs or []
        self.reviews = reviews or []
        self.statuses = statuses or []
        self.previous_attempt = previous_attempt or {}
        self.fail = fail

    def graphql(self, _query, variables):
        if self.fail == "checks":
            raise RuntimeError("checks denied")
        index = int(variables.get("cursor") or 0)
        nodes = self.pages[index] if self.pages else self.checks
        has_next = bool(self.pages and index + 1 < len(self.pages))
        return {"repository": {"pullRequest": {
            **PULL_REQUEST,
            "headRefOid": self.response_head,
            "reviews": {"nodes": self.reviews},
            "commits": {"nodes": [{"commit": {"pushedDate": HEAD_COMMITTED_AT,
                                                  "committedDate": HEAD_COMMITTED_AT,
                                                  "statusCheckRollup": {"contexts": {
                "nodes": nodes,
                "pageInfo": {"hasNextPage": has_next, "endCursor": str(index + 1)},
            }}}}]},
        }}}

    def paginate(self, path, *, key=None, params=None):
        if "/rules/branches/" in path:
            if self.fail == "rules":
                raise RuntimeError("rules denied")
            return [
                {"type": "required_status_checks", "parameters": {"required_status_checks": self.requirements}},
                {"type": "workflows", "parameters": {"workflows": self.workflows}},
            ]
        if path.endswith("/comments"):
            return self.comments
        if path.endswith("/actions/runs"):
            return self.runs
        if path.endswith("/statuses"):
            return self.statuses
        raise AssertionError(path)

    def request(self, method, path, body=None):
        if "/branches/" in path:
            return {"protection": {"enabled": True, "required_status_checks": {
                "checks": [{"context": item["context"], "app_id": item.get("app_id")}
                           for item in self.classic]
            }}}
        if path.startswith("/repositories/"):
            return {"full_name": "example/repo", "default_branch": "master"}
        if "/attempts/" in path:
            return self.previous_attempt
        raise AssertionError((method, path, body))


def inspect(*, now=None, comments_github=None, report_app_slug="github-actions", **kwargs):
    github = ReadinessGitHub(**kwargs)
    return load_readiness(
        github=github,
        comments_github=comments_github,
        owner="example",
        repo="repo",
        pull_request=PULL_REQUEST,
        report_app_slug=report_app_slug,
        rules_cache={},
        now=timestamp(HEAD_COMMITTED_AT) + 60 if now is None else now,
    )


class WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = (REPO_ROOT / ".github" / "workflows" / "sunrise-auto-draft-review-threads.yml").read_text(encoding="utf-8")
        cls.packaging_workflow_path = REPO_ROOT / ".github" / "workflows" / "sunrise-test-packaging.yml"
        cls.packaging_workflow = cls.packaging_workflow_path.read_text(encoding="utf-8-sig")
        cls.disabled_packaging_workflow = (REPO_ROOT / ".github" / "workflows" / "test-packaging.yml").read_text(encoding="utf-8-sig")
        cls.packaging_script = (CI_DIR / "check_packaging_paths.py").read_text(encoding="utf-8")
        cls.signal_workflow = (REPO_ROOT / ".github" / "workflows" / "sunrise-auto-draft-review-state-changed.yml").read_text(encoding="utf-8")
        cls.coderabbit = (REPO_ROOT / ".coderabbit.yaml").read_text(encoding="utf-8")

    def test_privileged_workflow_runs_python_with_app_token(self):
        self.assertIn("uses: actions/create-github-app-token@v3", self.workflow)
        self.assertIn("GH_TOKEN: ${{ steps.app-token.outputs.token }}", self.workflow)
        self.assertIn("run: python3 Tools/_sunrise/auto_draft/review_threads.py", self.workflow)
        self.assertIn("uses: actions/setup-python@ece7cb06caefa5fff74198d8649806c4678c61a1 # v6.3.0", self.workflow)
        self.assertIn('python-version: "3.11"', self.workflow)
        self.assertIn("# Sunrise edit start - фиксируем Python с поддержкой tomllib", self.workflow)
        self.assertIn("# Sunrise edit end", self.workflow)
        self.assertNotIn("actions/github-script", self.workflow)
        self.assertNotIn("AUTO_DRAFT_TOKEN", self.workflow)
        self.assertIn("sparse-checkout: Tools/_sunrise/auto_draft", self.workflow)
        self.assertIn("permissions: {}", self.workflow)
        self.assertIn("cancel-in-progress: false", self.workflow)
        self.assertIn("queue: max", self.workflow)
        self.assertIn('cron: "7/10 * * * *"', self.workflow)
        self.assertIn("github.event.action != 'converted_to_draft'", self.workflow)
        self.assertIn("github.event.action != 'ready_for_review'", self.workflow)
        self.assertIn("github.event.sender.type != 'Bot'", self.workflow)

    def test_review_events_and_coderabbit_settings_are_preserved(self):
        self.assertIn("pull_request_review:", self.signal_workflow)
        self.assertNotIn("secrets.", self.signal_workflow)
        self.assertIn('workflows: ["PR: Automatic Draft Management - Review Events", "Build & Test Debug", "YAML Linter"]', self.workflow)
        self.assertIn("request_changes_workflow: false", self.coderabbit)
        self.assertIn("fail_commit_status: true", self.coderabbit)
        self.assertIn("drafts: true", self.coderabbit)
        self.assertIn("auto_pause_after_reviewed_commits: 0", self.coderabbit)
        self.assertNotIn("AUTO_DRAFT_ALLOW_CODERABBIT_RATE_LIMIT", self.workflow)

    def test_packaging_uses_fast_gate_and_runs_for_drafts(self):
        pull_request = self.packaging_workflow.split("  pull_request:\n", 1)[1].split("\n\n", 1)[0]
        self.assertNotIn("paths:", pull_request)
        self.assertNotIn("ready_for_review", pull_request)
        self.assertNotIn("pull_request.draft", self.packaging_workflow)
        self.assertIn("name: Check packaging paths", self.packaging_workflow)
        self.assertIn("ref: ${{ github.sha }}", self.packaging_workflow)
        self.assertEqual(self.packaging_workflow.count("persist-credentials: false"), 2)
        self.assertIn("actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2", self.packaging_workflow)
        self.assertIn("space-wizards/submodule-dependency@548a726da00ca348ce9e1ea9f026da8f528caa71 # v0.1.5", self.packaging_workflow)
        self.assertIn("actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 # v4.6.2", self.packaging_workflow)
        self.assertNotIn("  pull_request_target:", self.packaging_workflow)
        self.assertIn("python3 Tools/_sunrise/ci/check_packaging_paths.py .github/workflows/sunrise-test-packaging.yml", self.packaging_workflow)
        self.assertNotIn("gh api", self.packaging_workflow)
        self.assertIn("gh\",\n            \"api\",", self.packaging_script)
        for pattern in load_workflow_paths(self.packaging_workflow_path):
            self.assertNotIn(pattern, self.packaging_script)
        self.assertIn("needs: [changes, package]", self.packaging_workflow)
        self.assertIn("CHANGES_RESULT: ${{ needs.changes.result }}", self.packaging_workflow)
        self.assertIn('if [[ "$PACKAGING_NEEDED" == "false" ]]', self.packaging_workflow)
        self.assertIn('if: ${{ always() }}', self.packaging_workflow)
        self.assertIn("if: needs.changes.outputs.packaging == 'true'", self.packaging_workflow)
        self.assertNotIn("PACKAGING_ACTOR", self.packaging_workflow)
        self.assertNotIn("Sunrise-Bot", self.packaging_workflow)
        self.assertNotIn("PJBot", self.packaging_workflow)
        self.assertLess(
            self.packaging_workflow.index('if [[ "$CHANGES_RESULT" != "success" ]]'),
            self.packaging_workflow.index('if [[ "$PACKAGING_NEEDED" == "false" ]]'),
        )
        self.assertIn("name: Test Packaging", self.packaging_workflow)

    def test_upstream_packaging_workflow_is_disabled(self):
        self.assertIn("name: Test Packaging (disabled)", self.disabled_packaging_workflow)
        self.assertIn("# Sunrise edit start - отключаем автоматический запуск upstream-сценария", self.disabled_packaging_workflow)
        self.assertIn("# Sunrise-Edit - upstream-сценарий оставлен только для ручного запуска", self.disabled_packaging_workflow)
        self.assertIn("# Sunrise edit end", self.disabled_packaging_workflow)
        self.assertIn("  workflow_dispatch:", self.disabled_packaging_workflow)
        self.assertNotIn("  pull_request:", self.disabled_packaging_workflow)
        self.assertNotIn("  push:", self.disabled_packaging_workflow)
        self.assertNotIn("concurrency:", self.disabled_packaging_workflow)
        self.assertNotIn("PJBot", self.disabled_packaging_workflow)
        self.assertIn("--configuration Release --no-build -- server --log-build", self.disabled_packaging_workflow)

    def test_toml_config_contains_localized_label_and_migration_name(self):
        config = load_config()
        label = config["label"]
        self.assertIn("автодрафт", label["name"])
        self.assertRegex(label["color"], r"^[A-Fa-f0-9]{6}$")
        self.assertIn("auto-draft: unresolved review", label["previous_names"])
        with open(AUTO_DRAFT_DIR / "config.toml", "rb") as config_file:
            self.assertEqual(tomllib.load(config_file), config)


class GitHubApiTests(unittest.TestCase):
    def test_graphql_and_rest_pagination(self):
        class Response:
            def __init__(self, data, link="", status=200):
                self.data = json.dumps(data).encode()
                self.headers = {"Link": link}
                self.status = status

            def __enter__(self):
                return self

            def __exit__(self, *_args):
                pass

            def read(self):
                return self.data

        graphql = Response({"data": {"repository": {"name": "repo"}}})
        first = Response([{"id": 1}], '<https://api.github.com/items?page=2>; rel="next"')
        second = Response([{"id": 2}])
        github = GitHub("token")
        with patch.object(github._opener, "open", side_effect=[graphql, first, second]) as open_request:
            self.assertEqual(github.graphql("query { viewer { login } }", {}),
                             {"repository": {"name": "repo"}})
            self.assertEqual(github.paginate("/items"), [{"id": 1}, {"id": 2}])
        self.assertEqual(open_request.call_count, 3)
        self.assertEqual(open_request.call_args_list[0].args[0].headers["Authorization"], "Bearer token")
        self.assertIn("page=2", open_request.call_args_list[2].args[0].full_url)

    def test_api_url_requires_https(self):
        with self.assertRaisesRegex(ValueError, "должен использовать HTTPS"):
            GitHub("token", "http://api.github.com")

    def test_graphql_errors_preserve_metadata_and_rate_limit(self):
        class Response:
            status = 200
            headers = {"X-RateLimit-Remaining": "0"}

            def __init__(self, data):
                self.data = json.dumps(data).encode()

            def __enter__(self):
                return self

            def __exit__(self, *_args):
                pass

            def read(self):
                return self.data

        github = GitHub("token")
        rate_limited = Response({"errors": [{"type": "RATE_LIMITED", "message": "limit"}]})
        empty = Response(None)
        with patch.object(github._opener, "open", side_effect=[rate_limited, empty]):
            with self.assertRaises(GitHubError) as caught:
                github.graphql("query { viewer { login } }", {})
            self.assertEqual(caught.exception.status, 429)
            self.assertEqual(caught.exception.headers["x-ratelimit-remaining"], "0")
            with self.assertRaisesRegex(GitHubError, "пустой ответ") as caught:
                github.graphql("query { viewer { login } }", {})
            self.assertEqual(caught.exception.status, 200)

    def test_redirects_keep_credentials_only_on_same_https_origin(self):
        handler = HTTPSRedirectHandler()
        request = Request("https://api.github.com/items", headers={"Authorization": "Bearer secret"})

        same_origin = handler.redirect_request(
            request, None, 302, "Found", {}, "https://api.github.com/next"
        )
        self.assertEqual(same_origin.get_header("Authorization"), "Bearer secret")
        other_origin = handler.redirect_request(
            request, None, 302, "Found", {}, "https://uploads.github.com/next"
        )
        self.assertIsNone(other_origin.get_header("Authorization"))
        with self.assertRaisesRegex(GitHubError, "не на HTTPS"):
            handler.redirect_request(request, None, 302, "Found", {}, "http://api.github.com/next")


class PackagingPathTests(unittest.TestCase):
    def test_paths_come_from_workflow_and_match_changed_files(self):
        workflow = REPO_ROOT / ".github" / "workflows" / "sunrise-test-packaging.yml"
        patterns = load_workflow_paths(workflow)

        for path in (
            "Content.Server/Foo.cs",
            "Content.Client/Content.Client.csproj",
            "Content.Packaging/Program.cs",
            "Content.Server.Database/Database.cs",
            "Content.Shared/Serialization.cs",
            "Content.Shared.Database/Database.cs",
            "Directory.Packages.props",
            "global.json",
            ".gitmodules",
            "MSBuild/Content.props",
            "nuget.config",
            "Resources/Prototypes/foo.yml",
            "RobustToolbox",
            "RobustToolbox/Robust.Shared/Foo.txt",
            "SpaceStation14.slnx",
            "Sunrise/Content.Sunrise.Interfaces.Shared/Foo.cs",
        ):
            with self.subTest(path=path):
                self.assertTrue(packaging_needed([path], patterns))

        self.assertFalse(packaging_needed([
            ".github/workflows/sunrise-test-packaging.yml",
            "README.md",
            "Tools/_sunrise/auto_draft/review_threads.py",
            "Tools/_sunrise/auto_draft/config.toml",
            "Other/RobustToolbox/foo.txt",
        ], patterns))


class PolicyTests(unittest.TestCase):
    def state(self, **overrides):
        state = {
            "is_draft": False,
            "has_marker": False,
            "latest_blocking_at": None,
            "latest_ready_at": None,
            "all_blocking_threads_resolved": False,
            "checks_ready": True,
            "code_rabbit_ready": True,
        }
        state.update(overrides)
        return decide_draft_state(**state)

    def test_policy_scenarios(self):
        cases = [
            ({"checks_ready": False, "keep_ready_during_rerun": True}, "keep"),
            ({"is_draft": True, "has_marker": True, "checks_ready": False}, "keep"),
            ({"latest_blocking_at": 20}, "draft"),
            ({"is_draft": True, "has_marker": True, "latest_blocking_at": 10,
              "all_blocking_threads_resolved": True}, "ready"),
            ({"has_marker": True, "latest_blocking_at": 10, "latest_ready_at": 20}, "cleanup"),
            ({"latest_blocking_at": 30, "latest_ready_at": 20}, "draft"),
            ({"code_rabbit_ready": False}, "draft"),
            ({"code_rabbit_ready": False, "keep_ready_during_rabbit_rerun": True}, "keep"),
            ({"code_rabbit_ready": False, "keep_ready_during_rabbit_rerun": True,
              "coderabbit_conversations_resolved": False}, "draft"),
            ({"checks_ready": False, "code_rabbit_ready": False,
              "keep_ready_during_rerun": True, "keep_ready_during_rabbit_rerun": True}, "keep"),
            ({"has_merge_conflicts": True, "latest_ready_at": 20}, "draft"),
            ({"is_draft": True, "has_marker": True, "has_merge_conflicts": True}, "keep"),
            ({"is_draft": True}, "keep"),
            ({"is_draft": True, "has_marker": True}, "ready"),
        ]
        for state, expected in cases:
            with self.subTest(state=state):
                self.assertEqual(self.state(**state), expected)

    def test_manual_draft_stays_manual_when_conflicts_resolve(self):
        self.assertEqual(self.state(is_draft=True, has_merge_conflicts=True), "keep")
        self.assertEqual(self.state(is_draft=True, has_merge_conflicts=False), "keep")

    def test_unknown_merge_state_preserves_current_state(self):
        cases = [
            {"has_marker": True},
            {"checks_ready": False},
            {"is_draft": True, "has_marker": True},
        ]
        for state in cases:
            with self.subTest(state=state):
                self.assertEqual(self.state(**state, merge_state_unknown=True), "keep")


class CodeRabbitConversationTests(unittest.TestCase):
    @staticmethod
    def thread(review_id, state="COMMENTED", resolved=False, login="coderabbitai"):
        return {
            "isResolved": resolved,
            "comments": {"nodes": [{"pullRequestReview": {
                "id": review_id,
                "state": state,
                "author": {"login": login},
            }}]},
        }

    def test_comment_mode_requires_every_coderabbit_conversation(self):
        rabbit = self.thread("rabbit-comment")
        human = self.thread("human-comment", login="human-reviewer")
        state = coderabbit_conversation_state([rabbit, human])
        self.assertEqual(state, {"total": 1, "unresolved": 1, "resolved": False})
        rabbit["isResolved"] = True
        self.assertEqual(
            coderabbit_conversation_state([rabbit, human]),
            {"total": 1, "unresolved": 0, "resolved": True},
        )

    def test_request_changes_review_is_reduced_to_its_conversations(self):
        thread = self.thread("rabbit-blocking", state="CHANGES_REQUESTED", resolved=True)
        state = coderabbit_conversation_state([thread])
        self.assertTrue(state["resolved"])

    def test_mixed_modes_count_all_coderabbit_threads(self):
        threads = [
            self.thread("rabbit-blocking", state="CHANGES_REQUESTED", resolved=True),
            self.thread("rabbit-comment", resolved=False),
            self.thread("human", resolved=False, login="reviewer"),
        ]
        state = coderabbit_conversation_state(threads)
        self.assertEqual(state, {"total": 2, "unresolved": 1, "resolved": False})
        threads[1]["isResolved"] = True
        self.assertTrue(coderabbit_conversation_state(threads)["resolved"])


class ReadinessTests(unittest.TestCase):
    def test_required_checks_and_latest_runs(self):
        self.assertTrue(inspect()["checks_ready"])
        result = inspect(checks=[RABBIT])
        self.assertFalse(result["checks_ready"])
        self.assertEqual(result["pending_checks"], ["Tests"])
        for conclusion in ("FAILURE", "CANCELLED", "TIMED_OUT", None):
            with self.subTest(conclusion=conclusion):
                result = inspect(checks=[{**CHECK, "conclusion": conclusion}, RABBIT])
                self.assertFalse(result["checks_ready"])
        for conclusion in ("NEUTRAL", "SKIPPED"):
            self.assertTrue(inspect(checks=[{**CHECK, "conclusion": conclusion}, RABBIT])["checks_ready"])
        self.assertTrue(inspect(checks=[{**CHECK, "conclusion": "FAILURE"}, {**CHECK, "databaseId": 2}, RABBIT])["checks_ready"])
        self.assertFalse(inspect(checks=[CHECK, {**CHECK, "databaseId": 2, "conclusion": None}, RABBIT])["checks_ready"])

    def test_skipped_packaging_is_accepted_but_missing_check_is_not(self):
        packaging = {**CHECK, "name": "Test Packaging", "conclusion": "SKIPPED"}
        requirement = {"context": "Test Packaging", "integration_id": 15368}
        self.assertTrue(inspect(checks=[packaging, RABBIT], requirements=[requirement])["checks_ready"])
        self.assertFalse(inspect(checks=[RABBIT], requirements=[requirement])["checks_ready"])

    def test_coderabbit_wait_is_bounded_from_head_commit_or_check_start(self):
        after_wait = timestamp(HEAD_COMMITTED_AT) + 30 * 60
        self.assertFalse(inspect(checks=[CHECK], now=after_wait - 1)["code_rabbit_ready"])
        absent = inspect(checks=[CHECK], now=after_wait)
        self.assertTrue(absent["code_rabbit_absent"])
        self.assertTrue(absent["code_rabbit_ready"])

        pending = {**RABBIT, "state": "PENDING", "createdAt": "2026-09-01T00:20:00Z"}
        self.assertFalse(inspect(checks=[CHECK, pending], now=after_wait)["code_rabbit_ready"])
        timed_out = inspect(checks=[CHECK, pending], now=timestamp(HEAD_COMMITTED_AT) + 50 * 60)
        self.assertTrue(timed_out["code_rabbit_timed_out"])
        self.assertFalse(timed_out["code_rabbit_absent"])

        for state in ("EXPECTED", "PENDING"):
            with self.subTest(state=state):
                skipped = {**pending, "state": state, "description": "Review skipped"}
                result = inspect(checks=[CHECK, skipped], now=after_wait)
                self.assertTrue(result["code_rabbit_ready"])
                self.assertTrue(result["code_rabbit_unavailable"])

    def test_coderabbit_terminal_failures_and_skip_messages_do_not_block(self):
        skipped = limited_comment("Review skipped\n\nAutomatic reviews are disabled on this target branch.")
        unavailable = inspect(checks=[CHECK], comments=[skipped])
        self.assertTrue(unavailable["code_rabbit_ready"])
        self.assertTrue(unavailable["code_rabbit_unavailable"])
        skipped_status = {**RABBIT, "description": "Review skipped: reviews are disabled for this base branch."}
        unavailable = inspect(checks=[CHECK, skipped_status])
        self.assertTrue(unavailable["code_rabbit_ready"])
        self.assertTrue(unavailable["code_rabbit_unavailable"])
        self.assertFalse(unavailable["code_rabbit_reviewed"])

        failed = {**RABBIT, "state": "FAILURE", "description": "Review failed"}
        unavailable = inspect(checks=[CHECK, failed])
        self.assertTrue(unavailable["code_rabbit_ready"])
        self.assertTrue(unavailable["code_rabbit_unavailable"])

        rabbit_run = {
            **CHECK,
            "name": "CodeRabbit",
            "databaseId": 9,
            "isRequired": False,
            "startedAt": HEAD_COMMITTED_AT,
            "checkSuite": {"app": {"databaseId": 1, "slug": "coderabbitai"}, "workflowRun": None},
        }
        for conclusion in ("FAILURE", "CANCELLED", "TIMED_OUT", "SKIPPED", "NEUTRAL", "ACTION_REQUIRED"):
            with self.subTest(conclusion=conclusion):
                result = inspect(checks=[CHECK, {**rabbit_run, "conclusion": conclusion}])
                self.assertTrue(result["code_rabbit_ready"])
                self.assertTrue(result["code_rabbit_unavailable"])

        copied = {**skipped, "user": {"type": "User", "login": "contributor"}}
        self.assertFalse(inspect(checks=[CHECK], comments=[copied])["code_rabbit_ready"])

    def test_separate_comment_client_and_report_app_slug(self):
        comments_github = ReadinessGitHub(comments=[limited_comment("Review in progress")])
        report_check = {
            **CHECK,
            "name": "Автодрафт",
            "databaseId": 99,
            "externalId": "auto-draft:1:hash",
            "checkSuite": {"app": {"slug": "autodraft"}, "workflowRun": None},
        }
        result = inspect(
            checks=[CHECK, RABBIT, report_check],
            comments_github=comments_github,
            report_app_slug="autodraft",
        )
        self.assertEqual(result["report_check"]["id"], 99)
        self.assertEqual(result["comments"], comments_github.comments)

    def test_coderabbit_rate_limit_is_always_non_blocking(self):
        comment = limited_comment("Rate limit exceeded")
        self.assertTrue(inspect(checks=[CHECK], comments=[comment])["code_rabbit_ready"])
        user_comment = {**comment, "user": {"type": "User", "login": "contributor"}}
        self.assertFalse(inspect(checks=[CHECK], comments=[user_comment])["code_rabbit_ready"])

    def test_previous_success_only_preserves_an_already_ready_pull_request(self):
        pending = {**RABBIT, "state": "PENDING", "description": "Review in progress",
                   "createdAt": "2026-09-01T00:01:00Z"}
        completed = {
            "context": "CodeRabbit",
            "state": "success",
            "description": "Review completed",
            "creator": {"type": "Bot", "login": "coderabbitai[bot]"},
        }
        result = inspect(checks=[CHECK, pending], statuses=[completed])
        self.assertFalse(result["code_rabbit_ready"])
        self.assertTrue(result["keep_ready_during_rabbit_rerun"])
        self.assertEqual(decide_draft_state(
            is_draft=True,
            has_marker=True,
            latest_blocking_at=None,
            latest_ready_at=None,
            all_blocking_threads_resolved=False,
            checks_ready=True,
            code_rabbit_ready=result["code_rabbit_ready"],
            keep_ready_during_rabbit_rerun=result["keep_ready_during_rabbit_rerun"],
        ), "keep")
        self.assertEqual(decide_draft_state(
            is_draft=False,
            has_marker=False,
            latest_blocking_at=None,
            latest_ready_at=None,
            all_blocking_threads_resolved=False,
            checks_ready=True,
            code_rabbit_ready=result["code_rabbit_ready"],
            keep_ready_during_rabbit_rerun=result["keep_ready_during_rabbit_rerun"],
        ), "keep")

    def test_pagination_failures_and_required_workflows(self):
        self.assertTrue(inspect(pages=[[], [CHECK, RABBIT]])["checks_ready"])
        with self.assertRaisesRegex(RuntimeError, "ПР изменился"):
            inspect(response_head="new-head")
        with self.assertRaisesRegex(RuntimeError, "rules denied"):
            inspect(fail="rules")

        workflow = {"path": ".github/workflows/required.yml", "repository_id": 7}
        run = {
            "id": 1,
            "head_sha": HEAD,
            "repository": {"id": 7},
            "path": workflow["path"] + "@master",
            "event": "pull_request",
            "status": "completed",
            "conclusion": "success",
            "pull_requests": [{"number": 1}],
        }
        self.assertFalse(inspect(workflows=[workflow])["checks_ready"])
        self.assertTrue(inspect(workflows=[workflow], runs=[run])["checks_ready"])
        self.assertFalse(inspect(workflows=[workflow], runs=[run, {**run, "id": 2, "conclusion": "failure"}])["checks_ready"])

    def test_ready_pull_stays_ready_while_successful_check_restarts(self):
        pending = {**CHECK, "databaseId": 2, "status": "IN_PROGRESS", "conclusion": None}
        result = inspect(checks=[CHECK, pending, RABBIT])
        self.assertFalse(result["checks_ready"])
        self.assertTrue(result["keep_ready_during_rerun"])


class ChecklistAndReportTests(unittest.TestCase):
    def state(self):
        return {
            "owner": "example",
            "repo": "repo",
            "number": 1,
            "app_slug": "autodraft",
            "feedback": [{"text": "Замечания reviewer", "done": False}],
            "readiness": inspect(),
        }

    def test_checklist_spoiler_and_sanitizing(self):
        body = build_checklist(**self.state())
        self.assertIn("<details>\n<summary>Показать обязательные проверки</summary>", body)
        self.assertGreater(body.index("Tests"), body.index("Показать обязательные проверки"))
        hostile = build_checklist(**{
            **self.state(),
            "feedback": [{"done": False, "text": "@someone #456\n- [x] <script>"}],
        })
        self.assertNotIn("@someone", hostile)
        self.assertNotIn("#456", hostile)
        self.assertNotIn("<script>", hostile)
        self.assertIn("он может ошибаться", body)

    def test_coderabbit_uses_one_checkbox_and_large_review_hint(self):
        state = self.state()
        state["feedback"] = []
        state["readiness"].update({
            "code_rabbit_review_ready": True,
            "code_rabbit_ready": False,
            "code_rabbit_conversations_total": 21,
            "code_rabbit_conversations_unresolved": 1,
        })
        body = build_checklist(**state)
        rabbit_checkboxes = [line for line in body.splitlines()
                            if line.startswith("- [") and "CodeRabbit" in line]
        self.assertEqual(len(rabbit_checkboxes), 1)
        self.assertIn("Осталось незакрытых: 1", rabbit_checkboxes[0])
        self.assertIn("> [!TIP]", body)
        self.assertIn("возможно, где-то осталось незамеченное незакрытое обсуждение", body)

        state["readiness"]["code_rabbit_conversations_total"] = 20
        self.assertNotIn("> [!TIP]", build_checklist(**state))
        state["readiness"].update({
            "code_rabbit_conversations_total": 21,
            "code_rabbit_conversations_unresolved": 0,
            "code_rabbit_ready": True,
        })
        self.assertNotIn("> [!TIP]", build_checklist(**state))

    def test_completed_coderabbit_checklist_text_is_not_stale(self):
        state = self.state()
        state["feedback"] = []
        state["readiness"].update({
            "code_rabbit_review_ready": True,
            "code_rabbit_ready": True,
            "code_rabbit_reviewed": True,
        })
        body = build_checklist(**state)
        self.assertIn("- [x] CodeRabbit проверил последнюю версию кода.", body)
        self.assertNotIn("Дождаться CodeRabbit", body)

    def test_checklist_survives_readiness_error(self):
        state = self.state()
        state["readiness"] = {"checks_ready": False, "code_rabbit_ready": False, "error": True}
        self.assertIn("Через 30 минут", build_checklist(**state))

    def test_coderabbit_fallbacks_are_visible_but_non_blocking(self):
        state = self.state()
        state["feedback"] = []
        state["readiness"].update({
            "code_rabbit_review_ready": True,
            "code_rabbit_ready": True,
            "code_rabbit_reviewed": False,
            "code_rabbit_timed_out": True,
        })
        body = build_checklist(**state)
        self.assertIn("- [x] CodeRabbit не завершил проверку за 30 минут", body)
        state["readiness"].update({"code_rabbit_timed_out": False, "code_rabbit_unavailable": True})
        self.assertIn("- [x] CodeRabbit завершился без результата", build_checklist(**state))

    def test_merge_conflict_item_appears_only_while_conflicting(self):
        state = self.state()
        self.assertNotIn("Решить конфликты слияния", build_checklist(**state))
        state["readiness"]["has_merge_conflicts"] = True
        body = build_checklist(**state)
        self.assertIn("- [ ] Решить конфликты слияния в IDE.", body)
        self.assertIn("> [!WARNING]", body)
        self.assertIn("GitHub не разрешит слить ПР", body)
        self.assertEqual(
            build_report(number=1, readiness=state["readiness"], action="draft")["title"],
            "Автодрафт: конфликты слияния",
        )
        state["readiness"]["has_merge_conflicts"] = False
        self.assertNotIn("Решить конфликты слияния", build_checklist(**state))
        state["readiness"]["merge_state_unknown"] = True
        self.assertNotIn("Решить конфликты слияния", build_checklist(**state))

    def test_checklist_is_updated_when_required_checks_change(self):
        class Comments:
            def __init__(self):
                self.comments = []
                self.calls = []

            def paginate(self, *_args, **_kwargs):
                return self.comments

            def request(self, method, path, body=None):
                self.calls.append((method, path, body))

        github = Comments()
        state = self.state()
        body = sync_checklist(github=github, **state)
        self.assertEqual(github.calls[-1][0], "POST")
        github.calls.clear()
        github.comments = [{"id": 7, "user": {"type": "Bot", "login": "autodraft[bot]"}, "body": body}]
        sync_checklist(github=github, **state)
        self.assertEqual(github.calls, [])
        changed = copy.deepcopy(state)
        changed["readiness"]["check_items"].append({"name": "New required test", "done": False})
        sync_checklist(github=github, **changed)
        self.assertEqual(github.calls[-1][0], "PATCH")
        self.assertIn("New required test", github.calls[-1][2]["body"])

    def test_checklist_ignores_unmarked_and_null_user_comments(self):
        class Comments:
            def __init__(self):
                self.calls = []

            def request(self, method, path, body=None):
                self.calls.append((method, path, body))

        github = Comments()
        comments = [
            {"id": 1, "user": None, "body": MARKER},
            {"id": 2, "user": {"type": "Bot", "login": "autodraft[bot]"}, "body": "другой комментарий"},
        ]
        sync_checklist(github=github, comments=comments, **self.state())
        self.assertEqual([call[0] for call in github.calls], ["POST"])

    def test_report_is_idempotent(self):
        class Checks:
            def __init__(self):
                self.check = None
                self.writes = 0

            def paginate(self, *_args, **_kwargs):
                return [self.check] if self.check else []

            def request(self, _method, _path, body=None):
                self.writes += 1
                self.check = {**body, "id": 9, "app": {"slug": "github-actions"}}

        class Core:
            def info(self, _message):
                pass

            def summary(self, _message):
                pass

        green = build_report(number=1, readiness=inspect(), action="ready")
        self.assertEqual(green["title"], "Автодрафт: всё готово")
        github = Checks()
        params = {"github": github, "core": Core(), "owner": "example", "repo": "repo",
                  "number": 1, "head": HEAD, "report": green}
        publish_report(**params)
        publish_report(**params)
        self.assertEqual(github.writes, 1)
        blocked = build_report(number=1, feedback=self.state()["feedback"], readiness=inspect(), action="draft")
        publish_report(**{**params, "report": blocked})
        self.assertEqual(github.writes, 2)
        self.assertEqual(github.check["id"], 9)
        rabbit_readiness = inspect()
        rabbit_readiness.update({
            "code_rabbit_ready": False,
            "code_rabbit_conversations_unresolved": 2,
        })
        self.assertEqual(
            build_report(number=1, readiness=rabbit_readiness, action="draft")["title"],
            "Автодрафт: нужно закрыть обсуждения CodeRabbit",
        )
        rabbit_readiness.update({
            "code_rabbit_conversations_unresolved": 0,
            "code_rabbit_ready": True,
            "code_rabbit_reviewed": False,
            "code_rabbit_unavailable": True,
        })
        report = build_report(number=1, readiness=rabbit_readiness, action="ready")
        self.assertEqual(report["title"], "Автодрафт: всё готово")
        self.assertIn("CodeRabbit завершился без результата", report["summary"])

    def test_report_handles_startup_failure_and_null_check_fields(self):
        readiness = inspect()
        readiness["checks_ready"] = False
        readiness["check_items"] = [{"name": "Startup", "done": False, "result": "STARTUP_FAILURE"}]
        report = build_report(number=1, readiness=readiness, action="draft")
        self.assertIn("не удалось запустить проверку", report["summary"])

        class Checks:
            def __init__(self):
                self.created = False

            def paginate(self, *_args, **_kwargs):
                return [{"id": 1, "external_id": None, "app": None}]

            def request(self, method, _path, _body=None):
                self.created = method == "POST"

        class Core:
            def info(self, _message):
                pass

            def summary(self, _message):
                pass

        github = Checks()
        publish_report(
            github=github,
            core=Core(),
            owner="example",
            repo="repo",
            number=1,
            head=HEAD,
            report=report,
        )
        self.assertTrue(github.created)


class RuntimeTests(unittest.TestCase):
    def test_invalid_config_is_rejected(self):
        config = load_config()
        config["label"]["color"] = "bad-color"
        context = {"owner": "example", "repo": "repo", "event_name": "schedule", "payload": {}}
        with self.assertRaisesRegex(RuntimeError, "config.toml"):
            AutoDraft(github=object(), context=context, core=object(), config=config)

    def test_event_targeting(self):
        class Targets:
            def paginate(self, path, **_kwargs):
                if path.endswith("/commits/sha/pulls"):
                    return [{"number": 3, "state": "open"}]
                if path.endswith("/pulls"):
                    return [{"number": 1}, {"number": 2}]
                raise AssertionError(path)

        class Core:
            def info(self, _message):
                pass

        with patch.dict(os.environ, {"AUTO_DRAFT_APP_SLUG": "autodraft"}):
            schedule = AutoDraft(github=Targets(), context={
                "owner": "example", "repo": "repo", "event_name": "schedule", "payload": {},
            }, core=Core())
            self.assertEqual(schedule.target_pull_request_numbers(), [1, 2])
            status = AutoDraft(github=Targets(), context={
                "owner": "example", "repo": "repo", "event_name": "status", "payload": {"sha": "sha"},
            }, core=Core())
            self.assertEqual(status.target_pull_request_numbers(), [3])

    def test_full_sync_converts_unresolved_coderabbit_comment_to_draft(self):
        class RuntimeGitHub:
            def __init__(self):
                review = {
                    "id": "R1",
                    "state": "COMMENTED",
                    "submittedAt": "2026-09-01T00:00:00Z",
                    "authorCanPushToRepository": False,
                    "author": {"login": "coderabbitai"},
                }
                self.pull = {
                    "id": "PR1",
                    "number": 1,
                    "state": "OPEN",
                    "isDraft": False,
                    "headRefOid": HEAD,
                    "baseRefName": "master",
                    "labels": {"nodes": [], "pageInfo": {"hasNextPage": False, "endCursor": None}},
                    "latestOpinionatedReviews": {"nodes": [review], "pageInfo": {"hasNextPage": False, "endCursor": None}},
                    "reviewThreads": {"nodes": [{
                        "isResolved": False,
                        "comments": {"nodes": [{"pullRequestReview": {
                            "id": "R1", "state": "COMMENTED", "author": {"login": "coderabbitai"},
                        }}]},
                    }], "pageInfo": {"hasNextPage": False, "endCursor": None}},
                    "timelineItems": {"nodes": []},
                }
                self.actions = []

            def graphql(self, query, _variables):
                if "mutation" in query:
                    self.actions.append("draft" if "convertPullRequestToDraft" in query else "ready")
                    return {}
                if "query Readiness" in query:
                    return {"repository": {"pullRequest": {
                        **PULL_REQUEST,
                        "reviews": {"nodes": []},
                        "commits": {"nodes": [{"commit": {
                            "pushedDate": HEAD_COMMITTED_AT,
                            "committedDate": HEAD_COMMITTED_AT,
                            "statusCheckRollup": {"contexts": {
                            "nodes": [RABBIT],
                            "pageInfo": {"hasNextPage": False, "endCursor": None},
                        }}}}]},
                    }}}
                return {"repository": {"pullRequest": copy.deepcopy(self.pull)}}

            def paginate(self, path, *, key=None, params=None):
                if "/rules/branches/" in path or path.endswith("/comments"):
                    return []
                raise AssertionError(path)

            def request(self, method, path, body=None):
                if method == "GET" and "/labels/" in path:
                    return load_config()["label"]
                if method == "GET" and "/branches/" in path:
                    return {}
                if method == "GET" and path.endswith("/pulls/1"):
                    return {"head": {"sha": HEAD}, "base": {"ref": "master"},
                            "draft": self.pull["isDraft"], "state": "open"}
                if method == "POST" and path.endswith("/comments"):
                    self.actions.append("comment")
                    return {}
                if method == "POST" and path.endswith("/labels"):
                    self.actions.append("label")
                    return {}
                if method == "POST" and path.endswith("/check-runs"):
                    self.actions.append("check")
                    return {}
                raise AssertionError((method, path, body))

        class Core:
            def info(self, _message):
                pass

            def warning(self, _message):
                pass

            def error(self, _message):
                pass

            def start_group(self, _message):
                pass

            def end_group(self):
                pass

            def summary(self, _message):
                pass

        github = RuntimeGitHub()
        context = {
            "owner": "example",
            "repo": "repo",
            "event_name": "pull_request_target",
            "payload": {"pull_request": {"number": 1}},
            "run_id": "7",
        }
        with patch.dict(os.environ, {"AUTO_DRAFT_APP_SLUG": "autodraft"}):
            AutoDraft(github=github, context=context, core=Core()).run()
        self.assertEqual(github.actions, ["comment", "label", "draft", "check"])

        github = RuntimeGitHub()
        github.pull.update({"isDraft": True, "mergeable": "CONFLICTING"})
        with patch.dict(os.environ, {"AUTO_DRAFT_APP_SLUG": "autodraft"}):
            AutoDraft(github=github, context=context, core=Core()).run()
        self.assertEqual(github.actions, ["comment", "check"])

        github = RuntimeGitHub()
        github.pull["latestOpinionatedReviews"]["nodes"][0].update({
            "state": "CHANGES_REQUESTED",
            "authorCanPushToRepository": True,
        })
        github.pull["reviewThreads"]["nodes"][0]["isResolved"] = True
        thread_review = github.pull["reviewThreads"]["nodes"][0]["comments"]["nodes"][0]["pullRequestReview"]
        thread_review["state"] = "CHANGES_REQUESTED"
        with patch.dict(os.environ, {"AUTO_DRAFT_APP_SLUG": "autodraft"}):
            AutoDraft(github=github, context=context, core=Core()).run()
        self.assertNotIn("draft", github.actions)


if __name__ == "__main__":
    unittest.main()
