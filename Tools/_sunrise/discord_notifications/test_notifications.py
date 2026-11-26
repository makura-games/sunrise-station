import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

import requests

from discord_notifications.config import load_config
from discord_notifications.deliver import collect, deliver_pending, enqueue
from discord_notifications.formatting import format_event
from discord_notifications.github import GitHubError
from discord_notifications.state import (
    ARTIFACTS,
    prune,
    restore,
    snapshots,
    trim_history,
)
from discord_notifications.transport import (
    DiscordDeliveryUncertainError,
    send_message,
)


CONFIG = load_config(Path(__file__).with_name("config.toml"))


class FakeGitHub:
    root = "/repos/example/repository"
    repository = "example/repository"

    def __init__(self, artifacts, runs, jobs=None, documents=None):
        self.artifacts = artifacts
        self.runs = runs
        self.jobs = jobs or []
        self.documents = documents or {}
        self.deleted = []

    def json(self, method, path, **kwargs):
        if path == self.root:
            return {"default_branch": "master"}
        if method == "DELETE" and "/actions/artifacts/" in path:
            self.deleted.append(int(path.rsplit("/", 1)[-1]))
            return None
        if "/actions/runs/" in path:
            return self.runs[int(path.rsplit("/", 1)[-1])]
        raise AssertionError((method, path, kwargs))

    def pages(self, path, key=None, **params):
        if path.endswith("/actions/artifacts"):
            yield from [
                item for item in self.artifacts if item["name"] == params["name"]
            ]
            return
        if path.endswith("/jobs"):
            yield from self.jobs
            return
        raise AssertionError((path, key, params))

    def artifact_json(self, artifact_id, filename):
        if artifact_id in self.documents:
            document = self.documents[artifact_id]
            if isinstance(document, Exception):
                raise document
            return document
        artifact_data = next(
            item for item in self.artifacts if item["id"] == artifact_id
        )
        return state_document(
            artifact_data["workflow_run"]["id"],
            artifact_data["workflow_run"].get("run_attempt", 1),
        )


def state_document(run_id, run_attempt):
    return {
        "version": 1,
        "cursor": "2026-09-12T00:00:00Z",
        "seen": {},
        "pending": {},
        "commit_comments": {},
        "messages": {},
        "checkpoint": {"run_id": run_id, "run_attempt": run_attempt},
    }


def artifact(identifier, name, run_id, created_at, run_attempt=None):
    result = {
        "id": identifier,
        "name": name,
        "created_at": created_at,
        "expired": False,
        "workflow_run": {"id": run_id, "head_branch": "master"},
    }
    if run_attempt is not None:
        result["workflow_run"]["run_attempt"] = run_attempt
    return result


def run(identifier, created_at, conclusion="success", run_attempt=1):
    return {
        "id": identifier,
        "created_at": created_at,
        "run_attempt": run_attempt,
        "workflow_id": 10,
        "head_repository": {"full_name": "example/repository"},
        "event": "workflow_run",
        "conclusion": conclusion,
    }


class StateTests(unittest.TestCase):
    @patch("discord_notifications.state.HISTORY_LIMIT", 2)
    def test_history_is_bounded_and_keeps_recent_records(self):
        document = {
            "messages": {"old": ["1"], "middle": ["2"], "new": ["3"]},
            "commit_comments": {
                "old": "2026-09-10T00:00:00Z",
                "new": "2026-09-12T00:00:00Z",
                "middle": "2026-09-11T00:00:00Z",
            },
        }

        trim_history(document)

        self.assertEqual(list(document["messages"]), ["middle", "new"])
        self.assertEqual(set(document["commit_comments"]), {"middle", "new"})

    def test_snapshots_use_run_time_and_prefer_final_state(self):
        github = FakeGitHub(
            [
                artifact(900, ARTIFACTS[0], 1, "2026-09-12T00:00:01Z"),
                artifact(100, ARTIFACTS[1], 2, "2026-09-12T00:01:02Z"),
                artifact(950, ARTIFACTS[0], 2, "2026-09-12T00:01:01Z"),
            ],
            {
                1: run(1, "2026-09-12T00:00:00Z"),
                2: run(2, "2026-09-12T00:01:00Z"),
            },
        )

        result = snapshots(github, {"workflow_id": 10})

        self.assertEqual(result[0]["id"], 100)
        self.assertEqual(result[0]["name"], ARTIFACTS[1])

    def test_snapshots_keep_attempt_from_each_artifact_checkpoint(self):
        github = FakeGitHub(
            [
                artifact(200, ARTIFACTS[1], 7, "2026-09-12T00:01:00Z"),
                artifact(100, ARTIFACTS[1], 7, "2026-09-12T00:02:00Z"),
            ],
            {7: run(7, "2026-09-12T00:00:00Z", run_attempt=2)},
            documents={
                200: state_document(7, 1),
                100: state_document(7, 2),
            },
        )

        result = snapshots(github, {"workflow_id": 10})

        self.assertEqual(
            [(item["id"], item["_run_attempt"]) for item in result],
            [(100, 2), (200, 1)],
        )

    def test_snapshots_ignores_unreadable_or_missing_artifacts(self):
        github = FakeGitHub(
            [
                artifact(12518695125, ARTIFACTS[0], 1, "2026-09-12T00:00:01Z"),
                artifact(12526209172, ARTIFACTS[1], 2, "2026-09-12T00:01:02Z"),
            ],
            {
                1: run(1, "2026-09-12T00:00:00Z"),
                2: run(2, "2026-09-12T00:01:00Z"),
            },
            documents={
                12518695125: GitHubError("Не удалось скачать файл события"),
            },
        )

        result = snapshots(github, {"workflow_id": 10})

        self.assertEqual(
            [item["id"] for item in result], [12526209172, 12518695125]
        )
        self.assertIsNotNone(result[0]["_document"])
        self.assertIsNone(result[1]["_document"])

    def test_restore_skips_unreadable_snapshot_and_uses_previous(self):
        github = FakeGitHub(
            [
                artifact(100, ARTIFACTS[1], 2, "2026-09-12T00:02:00Z"),
                artifact(200, ARTIFACTS[1], 1, "2026-09-12T00:01:00Z"),
            ],
            {
                1: run(1, "2026-09-12T00:01:00Z"),
                2: run(2, "2026-09-12T00:02:00Z"),
            },
            documents={
                100: GitHubError("Повреждённый архив артефакта"),
                200: {
                    **state_document(1, 1),
                    "pending": {"run:1": {"marker": "restored_from_200"}},
                },
            },
        )

        with tempfile.TemporaryDirectory() as directory:
            target_path = Path(directory) / "state.json"
            restore(
                github,
                {"id": 3, "run_attempt": 1, "workflow_id": 10},
                target_path,
                24,
            )
            saved = json.loads(target_path.read_text(encoding="utf-8"))
            self.assertEqual(saved["checkpoint"]["run_id"], 3)
            self.assertIn("run:1", saved["pending"])
            self.assertEqual(
                saved["pending"]["run:1"]["marker"], "restored_from_200"
            )

    def test_restore_fails_when_all_snapshots_unreadable(self):
        github = FakeGitHub(
            [
                artifact(100, ARTIFACTS[1], 1, "2026-09-12T00:01:00Z"),
            ],
            {
                1: run(1, "2026-09-12T00:01:00Z"),
            },
            documents={
                100: GitHubError("Повреждённый архив"),
            },
        )

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "повреждены или недоступны"):
                restore(
                    github,
                    {"id": 2, "run_attempt": 1, "workflow_id": 10},
                    Path(directory) / "state.json",
                    24,
                )

    def test_restore_refuses_ambiguous_pre_send_snapshot(self):
        github = FakeGitHub(
            [artifact(1, ARTIFACTS[0], 2, "2026-09-12T00:01:01Z")],
            {2: run(2, "2026-09-12T00:01:00Z")},
            jobs=[
                {
                    "steps": [
                        {"name": "Отправить очередь в Discord", "conclusion": "success"}
                    ]
                }
            ],
        )
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "повтор запрещён"):
                restore(
                    github,
                    {"id": 3, "run_attempt": 1, "workflow_id": 10},
                    Path(directory) / "state.json",
                    24,
                )

    def test_restore_rejects_checkpoint_from_another_attempt(self):
        github = FakeGitHub(
            [
                artifact(
                    1,
                    ARTIFACTS[1],
                    2,
                    "2026-09-12T00:01:01Z",
                    run_attempt=2,
                )
            ],
            {2: run(2, "2026-09-12T00:01:00Z", run_attempt=2)},
            documents={1: state_document(2, 1)},
        )

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "другому запуску или попытке"):
                restore(
                    github,
                    {"id": 3, "run_attempt": 1, "workflow_id": 10},
                    Path(directory) / "state.json",
                    24,
                )

    def test_prune_deletes_old_runs_instead_of_low_artifact_ids(self):
        artifacts = [
            artifact(700, ARTIFACTS[0], 1, "2026-09-12T00:00:01Z"),
            artifact(800, ARTIFACTS[0], 2, "2026-09-12T00:01:01Z"),
            artifact(900, ARTIFACTS[0], 3, "2026-09-12T00:02:01Z"),
            artifact(100, ARTIFACTS[0], 4, "2026-09-12T00:03:01Z"),
            artifact(50, ARTIFACTS[1], 4, "2026-09-12T00:03:02Z"),
        ]
        github = FakeGitHub(
            artifacts,
            {
                number: run(number, f"2026-09-12T00:0{number - 1}:00Z")
                for number in range(1, 5)
            },
        )

        prune(github, {"workflow_id": 10, "id": 4}, 50)

        self.assertEqual(github.deleted, [700])


class DeliveryTests(unittest.TestCase):
    @patch("discord_notifications.deliver.split_message")
    @patch("discord_notifications.deliver.format_event")
    def test_edit_without_message_id_is_not_published(self, format_event_mock, split_mock):
        message = {"embeds": [{"title": "PR"}]}
        format_event_mock.return_value = (message, "pull_request/edited · #1")
        split_mock.return_value = [message]
        state = SimpleNamespace(
            github=SimpleNamespace(repository="example/repository"),
            data={"messages": {}, "pending": {}},
        )
        log = Mock()

        enqueue(
            state,
            "run:1",
            "pull_request_target",
            {
                "action": "edited",
                "repository": {"full_name": "example/repository"},
                "pull_request": {"number": 1},
            },
            CONFIG,
            log,
        )

        self.assertEqual(state.data["pending"], {})
        self.assertIn("исходное сообщение", log.call_args.args[0])

    @patch("discord_notifications.deliver.send_message")
    def test_uncertain_result_disables_automatic_retry(self, send_message_mock):
        send_message_mock.side_effect = DiscordDeliveryUncertainError("timeout")
        item = {
            "message": {"embeds": []},
            "explanation": "test",
            "created_at": "2026-09-12T00:00:00Z",
        }
        state = SimpleNamespace(
            data={"pending": {"run:1": item}},
            save=Mock(),
        )

        with patch.dict("os.environ", {"DISCORD_EVENTS_WEBHOOK": "set"}):
            failures = deliver_pending(state, CONFIG, Mock(), float("inf"))

        self.assertEqual(failures, 1)
        self.assertTrue(item["retry"]["manual"])
        state.save.assert_called_once()

    @patch("discord_notifications.deliver.send_message")
    def test_uncertain_message_waits_for_manual_retry(self, send_message_mock):
        state = SimpleNamespace(
            data={
                "pending": {
                    "run:1": {
                        "message": {"embeds": []},
                        "explanation": "test",
                        "created_at": "2026-09-12T00:00:00Z",
                        "retry": {"manual": True},
                    }
                }
            },
            save=Mock(),
        )

        with patch.dict("os.environ", {"DISCORD_EVENTS_WEBHOOK": "set"}):
            failures = deliver_pending(state, CONFIG, Mock(), float("inf"))

        self.assertEqual(failures, 0)
        send_message_mock.assert_not_called()

    def test_collect_marks_seen_when_run_fails_to_load_and_cannot_rerun(self):
        github = Mock()
        github.root = "/repos/example/repository"
        github.pages.return_value = [
            {
                "id": 42,
                "created_at": "2099-01-01T00:01:00Z",
                "status": "completed",
                "conclusion": "success",
                "run_attempt": 1,
            }
        ]
        github.event_artifact.side_effect = GitHubError("Повреждён артефакт события")
        state = SimpleNamespace(
            github=github,
            data={
                "cursor": "2099-01-01T00:00:00Z",
                "seen": {},
                "pending": {},
            },
            save=Mock(),
        )
        log = Mock()

        errors = collect(state, CONFIG, log, float("inf"))

        self.assertEqual(errors, 1)
        self.assertIn("run:42", state.data["seen"])
        self.assertEqual(state.data["seen"]["run:42"], "2099-01-01T00:01:00Z")
        github.json.assert_not_called()
        state.save.assert_called_once()

    def test_collect_reruns_and_does_not_mark_seen_when_rerun_possible(self):
        github = Mock()
        github.root = "/repos/example/repository"
        github.pages.return_value = [
            {
                "id": 43,
                "created_at": "2099-01-01T00:01:00Z",
                "status": "completed",
                "conclusion": "failure",
                "run_attempt": 1,
            }
        ]
        github.event_artifact.side_effect = GitHubError("Сбой скачивания")
        state = SimpleNamespace(
            github=github,
            data={
                "cursor": "2099-01-01T00:00:00Z",
                "seen": {},
                "pending": {},
            },
            save=Mock(),
        )
        log = Mock()

        errors = collect(state, CONFIG, log, float("inf"))

        self.assertEqual(errors, 1)
        self.assertNotIn("run:43", state.data["seen"])
        github.json.assert_called_once_with(
            "POST", "/repos/example/repository/actions/runs/43/rerun"
        )
        state.save.assert_called_once()

    def test_comment_title_does_not_repeat_repository_name(self):
        account = {
            "login": "user",
            "type": "User",
            "html_url": "https://github.com/user",
        }
        message, _ = format_event(
            "issue_comment",
            {
                "action": "created",
                "repository": {"full_name": "example/repository"},
                "sender": account,
                "issue": {
                    "number": 1,
                    "title": "Issue",
                    "body": "Body",
                    "html_url": "https://github.com/example/repository/issues/1",
                    "user": account,
                    "labels": [],
                },
                "comment": {
                    "id": 2,
                    "body": "Comment",
                    "html_url": "https://github.com/example/repository/issues/1#issuecomment-2",
                    "user": account,
                },
            },
            CONFIG,
        )

        self.assertNotIn("[repository]", message["embeds"][0]["title"])


class TransportTests(unittest.TestCase):
    @patch("discord_notifications.transport.requests.post")
    def test_new_message_timeout_is_not_retried_automatically(self, post):
        post.side_effect = requests.Timeout

        with self.assertRaises(DiscordDeliveryUncertainError):
            send_message(
                "https://discord.com/api/webhooks/1/token",
                {"content": "test"},
                attempts=6,
                retry_ambiguous_creates=False,
            )

        post.assert_called_once()


class WorkflowTests(unittest.TestCase):
    def test_capture_skips_bots_but_keeps_bot_merges(self):
        capture = (
            Path(__file__).resolve().parents[3]
            / ".github"
            / "workflows"
            / "sunrise-discord-events.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("github.event.sender.type != 'Bot'", capture)
        self.assertIn("github.event.pull_request.merged == true", capture)

    def test_delivery_does_not_queue_for_skipped_capture(self):
        delivery = (
            Path(__file__).resolve().parents[3]
            / ".github"
            / "workflows"
            / "sunrise-discord-delivery.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("github.event.workflow_run.conclusion == 'success'", delivery)
        self.assertNotIn("\nconcurrency:\n", delivery)
        self.assertIn("    concurrency:\n", delivery)


if __name__ == "__main__":
    unittest.main()
