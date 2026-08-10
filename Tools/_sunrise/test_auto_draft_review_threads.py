import json
import re
import subprocess
import textwrap
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = REPO_ROOT / ".github" / "workflows" / "auto-draft-review-threads.yml"
SIGNAL_WORKFLOW_PATH = (
    REPO_ROOT / ".github" / "workflows" / "auto-draft-review-state-changed.yml"
)
CODERABBIT_PATH = REPO_ROOT / ".coderabbit.yaml"
SCRIPT_PATH = REPO_ROOT / "Tools" / "_sunrise" / "auto_draft" / "review_threads.js"


class AutoDraftReviewThreadsWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.signal_workflow = SIGNAL_WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.coderabbit = CODERABBIT_PATH.read_text(encoding="utf-8")
        cls.script = SCRIPT_PATH.read_text(encoding="utf-8")

    def test_review_events_are_relayed_without_privileged_operations(self):
        self.assertIn('name: "PR: Review State Changed"', self.signal_workflow)
        self.assertRegex(
            self.signal_workflow,
            r"pull_request_review:\s+types: \[submitted, edited, dismissed\]",
        )
        self.assertNotIn("secrets.", self.signal_workflow)
        self.assertNotIn("actions/checkout", self.signal_workflow)
        self.assertNotIn("actions/github-script", self.signal_workflow)

        self.assertIn("workflow_run:", self.workflow)
        self.assertIn('workflows: ["PR: Review State Changed"]', self.workflow)
        self.assertNotIn("pull_request_review_comment:", self.workflow)
        self.assertNotRegex(self.workflow, r"(?m)^  pull_request_review:\s*$")

    def test_privileged_workflow_uses_organization_app_token(self):
        self.assertIn("uses: actions/create-github-app-token@v3", self.workflow)
        self.assertIn("client-id: ${{ vars.AUTO_DRAFT_APP_CLIENT_ID }}", self.workflow)
        self.assertIn(
            "private-key: ${{ secrets.AUTO_DRAFT_APP_PRIVATE_KEY }}",
            self.workflow,
        )
        self.assertIn("permission-contents: write", self.workflow)
        self.assertIn("permission-issues: write", self.workflow)
        self.assertIn("permission-pull-requests: write", self.workflow)
        self.assertIn("uses: actions/checkout@v6", self.workflow)
        self.assertIn("ref: ${{ github.event.repository.default_branch }}", self.workflow)
        self.assertIn("sparse-checkout: Tools/_sunrise/auto_draft/review_threads.js", self.workflow)
        self.assertIn("persist-credentials: false", self.workflow)
        self.assertIn("github-token: ${{ steps.app-token.outputs.token }}", self.workflow)
        self.assertNotIn("AUTO_DRAFT_TOKEN", self.workflow)
        self.assertNotIn("secrets.GITHUB_TOKEN", self.workflow)
        self.assertNotIn("continue-on-error", self.workflow)
        self.assertIn("const failures = [];", self.script)
        self.assertIn("for (const number of numbers)", self.script)

    def test_review_state_comes_from_regular_github_reviews(self):
        for expected in (
            "latestOpinionatedReviews",
            "authorCanPushToRepository",
            "pullRequestReview",
            "READY_FOR_REVIEW_EVENT",
        ):
            self.assertIn(expected, self.script)

        for obsolete in (
            "CODEOWNERS",
            "AUTO_DRAFT_COMMENT_MARKER",
            "createComment",
            "updateComment",
        ):
            self.assertNotIn(obsolete, self.script)

    def test_coderabbit_submits_real_review_decisions(self):
        self.assertRegex(
            self.coderabbit,
            r"(?m)^  request_changes_workflow: true$",
        )

    def test_policy_scenarios(self):
        match = re.search(
            r"^\s*// AUTO_DRAFT_POLICY_START\s*$\n"
            r"(?P<policy>.*?)"
            r"^\s*// AUTO_DRAFT_POLICY_END\s*$",
            self.script,
            re.DOTALL | re.MULTILINE,
        )
        self.assertIsNotNone(match, "Не найдены стабильные маркеры функции политики")
        policy = textwrap.dedent(match.group("policy"))
        cases = [
            {
                "name": "новое требование включает draft",
                "input": self.policy_input(latestBlockingAt=20),
                "expected": "draft",
            },
            {
                "name": "старый approve не отменяет новое требование",
                "input": self.policy_input(
                    latestBlockingAt=20,
                    latestApprovalAt=10,
                ),
                "expected": "draft",
            },
            {
                "name": "новый approve снимает автодрафт",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                    latestApprovalAt=20,
                ),
                "expected": "ready",
            },
            {
                "name": "закрытие всех обсуждений снимает автодрафт",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                    allBlockingThreadsResolved=True,
                ),
                "expected": "ready",
            },
            {
                "name": "частично закрытые обсуждения сохраняют draft",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                ),
                "expected": "keep",
            },
            {
                "name": "требование без обсуждений сохраняет draft",
                "input": self.policy_input(
                    isDraft=True,
                    hasMarker=True,
                    latestBlockingAt=10,
                ),
                "expected": "keep",
            },
            {
                "name": "ручной Ready удаляет служебную метку",
                "input": self.policy_input(
                    hasMarker=True,
                    latestBlockingAt=10,
                    latestReadyAt=20,
                ),
                "expected": "cleanup",
            },
            {
                "name": "новое требование после ручного Ready снова включает draft",
                "input": self.policy_input(
                    latestBlockingAt=30,
                    latestReadyAt=20,
                ),
                "expected": "draft",
            },
            {
                "name": "ручной draft без метки не переводится в Ready",
                "input": self.policy_input(isDraft=True),
                "expected": "keep",
            },
            {
                "name": "снятое требование убирает автодрафт",
                "input": self.policy_input(isDraft=True, hasMarker=True),
                "expected": "ready",
            },
            {
                "name": "обычный комментарий ничего не меняет",
                "input": self.policy_input(),
                "expected": "keep",
            },
        ]
        harness = f"""
{policy}
const cases = {json.dumps(cases, ensure_ascii=False)};
for (const testCase of cases) {{
  const actual = decideDraftState(testCase.input);
  if (actual !== testCase.expected) {{
    console.error(`${{testCase.name}}: ожидалось ${{testCase.expected}}, получено ${{actual}}`);
    process.exitCode = 1;
  }}
}}
"""
        result = subprocess.run(
            ["node", "-"],
            input=harness,
            text=True,
            capture_output=True,
            encoding="utf-8",
            check=False,
        )
        self.assertEqual(result.returncode, 0, result.stderr)

    @staticmethod
    def policy_input(**overrides):
        defaults = {
            "isDraft": False,
            "hasMarker": False,
            "latestBlockingAt": None,
            "latestApprovalAt": None,
            "latestReadyAt": None,
            "allBlockingThreadsResolved": False,
        }
        defaults.update(overrides)
        return defaults


if __name__ == "__main__":
    unittest.main()
