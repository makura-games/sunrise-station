"""Локальные проверки не обращаются к настоящему Discord или GitHub."""

import copy
import io
import json
import os
import time
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import Mock, patch

import requests
from discord_notifications import deliver, transport
from discord_notifications.config import load_config
from discord_notifications.formatting import format_event
from discord_notifications.github import GitHubError

ADDRESS = "https://discord.com/api/webhooks/123/test-token"
CONFIG = Path(__file__).with_name("config.toml")


def event_payload() -> dict:
    return {
        "repository": {"full_name": "owner/repo"},
        "sender": {"login": "Human", "type": "User"},
        "action": "opened",
        "pull_request": {
            "title": "Проверка",
            "body": "Описание",
            "number": 1,
            "html_url": "https://github.com/owner/repo/pull/1",
            "user": {"login": "Author", "type": "User"},
            "base": {"ref": "master"},
            "merged": False,
        },
    }


def response(status: int, body: dict | None = None) -> Mock:
    result = Mock(status_code=status, headers={})
    result.json.return_value = body or {}
    return result


class NotificationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = load_config(CONFIG)

    def test_pr_and_review_colors(self):
        scenarios = [
            ("pull_request_target", "closed", True, "", "merged"),
            ("pull_request_target", "closed", False, "", "closed"),
            (
                "pull_request_review",
                "submitted",
                False,
                "approved",
                "approved",
            ),
            (
                "pull_request_review",
                "submitted",
                False,
                "changes_requested",
                "changes_requested",
            ),
            (
                "pull_request_review",
                "dismissed",
                False,
                "approved",
                "dismissed",
            ),
        ]
        for event, action, merged, review, style in scenarios:
            with self.subTest(style=style):
                payload = event_payload()
                payload["action"] = action
                payload["pull_request"]["merged"] = merged
                payload["review"] = {"state": review}
                message, _ = format_event(event, payload, self.config)
                self.assertEqual(
                    int(self.config["styles"][style]["color"][1:], 16),
                    message["embeds"][0]["color"],
                )

    def test_all_previous_visible_event_categories(self):
        for event in (
            "pull_request",
            "pull_request_review",
            "pull_request_review_comment",
            "issue_comment",
            "issues",
            "discussion",
            "discussion_comment",
            "commit_comment",
            "fork",
            "watch",
        ):
            with self.subTest(event=event):
                payload = event_payload()
                payload["action"] = "created"
                message, _ = format_event(event, payload, self.config)
                self.assertIn("embeds", message)

    def test_bots_and_machine_users_but_not_human_pr_merges(self):
        for actor in (
            {"login": "robot[bot]", "type": "Bot"},
            {"login": "Sunrise-Bot", "type": "User"},
        ):
            payload = event_payload()
            payload["sender"] = actor
            self.assertIsNone(
                format_event("pull_request", payload, self.config)[0]
            )
            payload["action"] = "closed"
            payload["pull_request"]["merged"] = True
            self.assertIsNotNone(
                format_event("pull_request", payload, self.config)[0]
            )

    def test_large_unicode_and_untrusted_markdown(self):
        payload = event_payload()
        payload["pull_request"]["title"] = "😀" * 1000
        payload["pull_request"]["body"] = (
            "<!-- private template -->@everyone " + "😀" * 10000
        )
        message, _ = format_event("pull_request", payload, self.config)
        embed = message["embeds"][0]
        self.assertLessEqual(len(embed["title"].encode("utf-16-le")) // 2, 256)
        self.assertLessEqual(
            len(embed["description"].encode("utf-16-le")) // 2, 4096
        )
        self.assertNotIn("private template", embed["description"])
        self.assertIn("@everyone", embed["description"])
        self.assertEqual({"parse": []}, message["allowed_mentions"])

    def test_whitespace_cannot_bypass_discord_size_limits(self):
        payload = event_payload()
        payload["pull_request"]["body"] = " " * 20000 + "hello" + " " * 20000
        message, _ = format_event("pull_request", payload, self.config)
        self.assertLess(len(message["embeds"][0]["description"]), 1000)

    def test_large_push_and_per_commit_filter(self):
        payload = event_payload()
        payload.pop("action")
        payload["ref"] = "refs/heads/master"
        payload["commits"] = [
            {
                "message": f"Изменение {index}\n" + "full body" * 100,
                "author": {"username": "Human"},
            }
            for index in range(598)
        ] + [{"message": "Hidden", "author": {"username": "Sunrise-Bot"}}]
        message, _ = format_event("push", payload, self.config)
        description = message["embeds"][0]["description"]
        self.assertNotIn("Hidden", description)
        self.assertIn("full body", description)
        self.assertIn("598", message["embeds"][0]["title"])
        self.assertNotIn("Изменение 11", description)

    def test_transport_retries_network_server_and_rate_limit(self):
        replies = [
            requests.Timeout(ADDRESS),
            response(503),
            response(429, {"retry_after": 0.1}),
            response(200, {"id": "42"}),
        ]
        logs = []
        with (
            patch.object(
                transport.requests, "post", side_effect=replies
            ) as post,
            patch.object(transport.time, "sleep") as sleep,
        ):
            receipt = transport.send_message(
                ADDRESS, {"content": "Hello"}, report=logs.append
            )
        self.assertEqual("42", receipt["id"])
        self.assertEqual(4, post.call_count)
        self.assertEqual(0.1, sleep.call_args.args[0])
        self.assertNotIn("test-token", "\n".join(logs))

    def test_permanent_error_does_not_retry_and_redacts_response(self):
        with patch.object(
            transport.requests,
            "post",
            return_value=response(404, {"secret": ADDRESS}),
        ) as post:
            with self.assertRaises(transport.DiscordError) as raised:
                transport.send_message(ADDRESS, {"content": "Hello"})
        self.assertEqual(1, post.call_count)
        self.assertNotIn("test-token", str(raised.exception))

    def test_retry_deadline_and_invalid_delays(self):
        for value in (True, -1, None, "bad", float("nan")):
            self.assertEqual(
                1, transport.retry_after(response(429, {"retry_after": value}))
            )
        with (
            patch.object(
                transport.requests,
                "post",
                return_value=response(429, {"retry_after": 1000}),
            ),
            patch.object(transport.time, "sleep") as sleep,
        ):
            with self.assertRaises(transport.DiscordError):
                transport.send_message(
                    ADDRESS,
                    {},
                    deadline=time.monotonic() + 1,
                    report=lambda message: None,
                )
        sleep.assert_not_called()

    def test_legacy_suffix_components_and_explicit_mentions(self):
        payload = {
            "flags": 32768,
            "components": [],
            "allowed_mentions": {"roles": ["7"]},
        }
        original = copy.deepcopy(payload)
        with patch.object(
            transport.requests, "post", return_value=response(200)
        ) as post:
            transport.send_message(ADDRESS + "/github", payload, files=[])
        self.assertEqual(original, payload)
        self.assertIn("wait=true", post.call_args.args[0])
        self.assertIn("with_components=true", post.call_args.args[0])
        self.assertNotIn("/github", post.call_args.args[0])
        self.assertEqual(
            {"roles": ["7"]},
            json.loads(post.call_args.kwargs["data"]["payload_json"])[
                "allowed_mentions"
            ],
        )

    def test_destination_validation_prevents_secret_redirect(self):
        for address in (
            "https://evil.test/",
            ADDRESS.replace("https", "http"),
            ADDRESS + "/messages/123",
            ADDRESS.replace("discord.com", "discord.com@evil.test"),
        ):
            with self.subTest(address=address), self.assertRaises(ValueError):
                transport.webhook_url(address)

    def new_state(self):
        state = Mock()
        state.github.json.return_value = []
        state.github.pages.return_value = []
        state.github.repository = "owner/repo"
        state.data = {
            "pending": {},
            "seen": {},
            "cursor": "2026-01-01T00:00:00Z",
            "commit_comments": {},
        }
        return state

    def test_queue_survives_failure_and_resumes_per_destination(self):
        state = self.new_state()
        self.config["delivery"]["retry_interval"] = 0
        with patch.dict(os.environ, {"DISCORD_EVENTS_WEBHOOK": ADDRESS}):
            deliver.enqueue(
                state,
                "run:1",
                "pull_request",
                event_payload(),
                self.config,
                lambda message: None,
            )
            with patch.object(
                deliver,
                "send_message",
                side_effect=transport.DiscordError(503, "temporary"),
            ):
                self.assertEqual(
                    1,
                    deliver.deliver_pending(
                        state,
                        self.config,
                        lambda message: None,
                        time.monotonic() + 100,
                    ),
                )
            self.assertIn("run:1", state.data["pending"])
            with patch.object(
                deliver, "send_message", return_value={"id": "42"}
            ) as send:
                self.assertEqual(
                    0,
                    deliver.deliver_pending(
                        state,
                        self.config,
                        lambda message: None,
                        time.monotonic() + 100,
                    ),
                )
                self.assertEqual({}, state.data["pending"])
                self.assertEqual(1, send.call_count)

    def test_long_message_resumes_only_undelivered_parts(self):
        state = self.new_state()
        payload = event_payload()
        payload["pull_request"]["body"] = "Длинный текст\n" * 1000
        with patch.dict(os.environ, {"DISCORD_EVENTS_WEBHOOK": ADDRESS}):
            deliver.enqueue(
                state,
                "run:1",
                "pull_request",
                payload,
                self.config,
                lambda message: None,
            )
            count = len(state.data["pending"])
            self.assertGreater(count, 1)
            replies = [{"id": "42"}] + [
                transport.DiscordError(503, "temporary")
            ] * (count - 1)
            with patch.object(deliver, "send_message", side_effect=replies):
                deliver.deliver_pending(
                    state,
                    self.config,
                    lambda message: None,
                    time.monotonic() + 100,
                )
            self.assertEqual(count - 1, len(state.data["pending"]))
            with patch.object(
                deliver, "send_message", return_value={"id": "43"}
            ) as send:
                deliver.deliver_pending(
                    state,
                    self.config,
                    lambda message: None,
                    time.monotonic() + 100,
                    force_retry=True,
                )
            self.assertEqual(count - 1, send.call_count)
            self.assertFalse(state.data["pending"])

    def test_already_confirmed_destination_is_not_sent_again(self):
        state = self.new_state()
        deliver.enqueue(
            state,
            "run:1",
            "pull_request",
            event_payload(),
            self.config,
            lambda message: None,
        )
        state.data["pending"]["run:1"]["sent"]["primary"] = {
            "message_id": "42"
        }
        with patch.object(deliver, "send_message") as send:
            deliver.deliver_pending(
                state,
                self.config,
                lambda message: None,
                time.monotonic() + 100,
            )
        send.assert_not_called()

    def test_queue_rejects_other_repository(self):
        payload = event_payload()
        payload["repository"]["full_name"] = "other/repo"
        with self.assertRaises(ValueError):
            deliver.enqueue(
                self.new_state(),
                "1",
                "pull_request",
                payload,
                self.config,
                lambda message: None,
            )

    def test_checkpoint_failure_stops_further_sends(self):
        state = self.new_state()
        state.save.side_effect = GitHubError("HTTP 409")
        deliver.enqueue(
            state,
            "run:1",
            "pull_request",
            event_payload(),
            self.config,
            lambda message: None,
        )
        with (
            patch.dict(os.environ, {"DISCORD_EVENTS_WEBHOOK": ADDRESS}),
            patch.object(
                deliver, "send_message", return_value={"id": "42"}
            ) as send,
        ):
            with self.assertRaises(GitHubError):
                deliver.deliver_pending(
                    state,
                    self.config,
                    lambda message: None,
                    time.monotonic() + 100,
                )
        self.assertEqual(1, send.call_count)

    def test_log_cannot_inject_workflow_commands(self):
        stream = io.StringIO()
        with redirect_stdout(stream):
            journal = deliver.Journal()
            journal("Title\n::error::fake " + ADDRESS)
        self.assertNotIn("\n::error", stream.getvalue())
        self.assertNotIn("test-token", stream.getvalue())

    def test_failed_destination_does_not_resend_successful_destination(self):
        state = self.new_state()
        environment = {
            "DISCORD_EVENTS_WEBHOOK": ADDRESS,
            "DISCORD_EVENTS_WEBHOOK_SECONDARY": ADDRESS,
        }
        with patch.dict(os.environ, environment):
            deliver.enqueue(
                state,
                "run:1",
                "pull_request",
                event_payload(),
                self.config,
                lambda message: None,
            )
            with patch.object(
                deliver,
                "send_message",
                side_effect=[
                    {"id": "42"},
                    transport.DiscordError(503, "temporary"),
                ],
            ):
                deliver.deliver_pending(
                    state,
                    self.config,
                    lambda message: None,
                    time.monotonic() + 100,
                )
            with patch.object(
                deliver, "send_message", return_value={"id": "43"}
            ) as send:
                deliver.deliver_pending(
                    state,
                    self.config,
                    lambda message: None,
                    time.monotonic() + 100,
                    force_retry=True,
                )
            self.assertEqual(1, send.call_count)
            self.assertFalse(state.data["pending"])

    def test_unfinished_capture_holds_cursor_without_losing_ready_events(self):
        state = self.new_state()
        self.config["display"]["show_reactions"] = False
        state.github.root = "/repos/owner/repo"
        state.github.pages.return_value = [
            {
                "id": 2,
                "created_at": "2026-01-03T00:00:00Z",
                "status": "completed",
                "conclusion": "success",
                "event": "pull_request_target",
                "actor": {"login": "Human"},
            },
            {
                "id": 1,
                "created_at": "2026-01-02T00:00:00Z",
                "status": "queued",
            },
        ]
        state.github.event_artifact.return_value = event_payload()
        deliver.collect(
            state, self.config, lambda message: None, time.monotonic() + 100
        )
        self.assertEqual("2026-01-02T00:00:00Z", state.data["cursor"])
        self.assertIn("run:2", state.data["pending"])
        self.assertNotIn("run:1", state.data["seen"])

    def test_commit_comments_are_polled_and_not_repeated(self):
        state = self.new_state()
        state.github.root = "/repos/owner/repo"
        comment = {
            "id": 12,
            "created_at": "2026-01-02T00:00:00Z",
            "updated_at": "2026-01-02T00:00:00Z",
            "body": "Comment",
            "user": {"login": "Human", "type": "User"},
        }
        state.github.pages.return_value = [comment]
        for _ in range(2):
            deliver.collect_commit_comments(
                state, self.config, lambda message: None
            )
        self.assertEqual(1, len(state.data["pending"]))

    def test_source_actor_mismatch_is_not_delivered(self):
        state = self.new_state()
        state.github.root = "/repos/owner/repo"
        state.github.pages.return_value = [
            {
                "id": 1,
                "created_at": "2026-01-02T00:00:00Z",
                "status": "completed",
                "conclusion": "success",
                "event": "pull_request_target",
                "actor": {"login": "Other"},
            }
        ]
        state.github.event_artifact.return_value = event_payload()
        self.assertEqual(
            1,
            deliver.collect(
                state,
                self.config,
                lambda message: None,
                time.monotonic() + 100,
            ),
        )
        self.assertFalse(state.data["pending"])
        self.assertFalse(state.data["seen"])

    def test_custom_color_and_event_filter(self):
        self.config["styles"]["opened"]["color"] = "#123456"
        message, _ = format_event("pull_request", event_payload(), self.config)
        self.assertEqual(0x123456, message["embeds"][0]["color"])
        self.config["events"]["pull_request"] = []
        self.assertIsNone(
            format_event("pull_request", event_payload(), self.config)[0]
        )


if __name__ == "__main__":
    unittest.main()
