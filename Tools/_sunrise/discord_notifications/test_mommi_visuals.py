"""Контракт карточек из основной ветки v2 MoMMI, без его зависимостей."""

import copy
import unittest
from unittest.mock import Mock

from discord_notifications.config import load_config
from discord_notifications.content import (
    prepare_body,
    split_body,
    split_message,
    units,
)
from discord_notifications.formatting import format_event
from discord_notifications.github import GitHubError
from discord_notifications.metadata import enrich_event
from discord_notifications.test_notifications import CONFIG, event_payload


class MoMMIVisualTests(unittest.TestCase):
    def setUp(self):
        self.config = load_config(CONFIG)
        self.payload = event_payload()
        self.payload["sender"].update(
            {
                "html_url": "https://github.com/Human",
                "avatar_url": "https://avatars.githubusercontent.com/u/1",
            }
        )
        self.payload["pull_request"].update(
            {
                "title": "A **formatted** title",
                "body": "**Description**<!-- hidden -->\nSecond line",
                "state": "open",
                "head": {"sha": "a" * 40},
                "reactions": {"+1": 3, "-1": 1},
                "mergeable": False,
            }
        )
        self.payload["pull_request"]["user"].update(
            {
                "avatar_url": "https://avatars.githubusercontent.com/u/2",
            }
        )
        self.payload["_discord_checks"] = [
            {"name": "Build", "status": "completed", "conclusion": "success"},
            {"name": "Tests", "status": "in_progress", "conclusion": None},
        ]
        self.payload["_discord_pr_status"] = {
            "state": "OPEN",
            "isDraft": False,
            "reviewDecision": "REVIEW_REQUIRED",
        }

    def render(self, event="pull_request"):
        return format_event(event, self.payload, self.config)[0]["embeds"][0]

    def test_pr_matches_mommi_card(self):
        self.assertEqual(
            {
                "title": "<:propen:896496081960054827> A **formatted** title",
                "url": "https://github.com/owner/repo/pull/1",
                "color": 0x6CC644,
                "description": (
                    "**Description**\nSecond line\n＋ 3   − 1\u200b"
                ),
                "author": {
                    "name": "Human",
                    "url": "https://github.com/Human",
                    "icon_url": "https://avatars.githubusercontent.com/u/1",
                },
                "footer": {"text": "PR: Open · Review: Waiting for review"},
                "fields": [
                    {
                        "name": "status",
                        "value": "🚨CONFLICTS🚨",
                        "inline": True,
                    }
                ],
            },
            self.render(),
        )
        embeds = format_event("pull_request", self.payload, self.config)[0][
            "embeds"
        ]
        self.assertEqual(2, len(embeds))
        self.assertEqual("Checks", embeds[1]["title"])
        self.assertEqual("✓ Build\n⏳ Tests", embeds[1]["description"])
        self.assertNotIn("image", embeds[1])

    def test_closed_and_merged_icons_and_palette(self):
        self.payload["action"] = "closed"
        for merged, color, emoji in (
            (False, 0xFF4444, "<:prclosed:896496081976827986>"),
            (True, 0x6E5494, "<:prmerge:896496082324979772>"),
        ):
            with self.subTest(merged=merged):
                self.payload["pull_request"]["merged"] = merged
                embed = self.render()
                self.assertEqual("Human", embed["author"]["name"])
                self.assertEqual(color, embed["color"])
                self.assertEqual(
                    emoji + " A **formatted** title", embed["title"]
                )

    def test_reviews_share_layout_and_keep_aggregate_github_status(self):
        self.payload["action"] = "submitted"
        self.payload["_discord_pr_status"]["reviewDecision"] = (
            "CHANGES_REQUESTED"
        )
        for state, label in (
            ("approved", "Approved"),
            ("changes_requested", "Changes requested"),
            ("commented", "Comment"),
        ):
            self.payload["review"] = {
                "state": state,
                "body": "Текст ревью",
                "user": self.payload["sender"],
            }
            message = format_event(
                "pull_request_review", self.payload, self.config
            )[0]
            embed = message["embeds"][0]
            self.assertEqual(1, len(message["embeds"]))
            self.assertEqual("Human", embed["author"]["name"])
            self.assertEqual(
                self.config["icons"][self.config["reviews"][state]["icon"]]
                + " "
                + label
                + " · A **formatted** title",
                embed["title"],
            )
            self.assertEqual("Текст ревью", embed["description"])
            self.assertNotIn("fields", embed)
            self.assertNotIn("thumbnail", embed)
            self.assertEqual(
                "PR: Open · Review: Changes requested", embed["footer"]["text"]
            )
            self.assertNotIn("_files", message)
        self.payload["_discord_pr_status"]["isDraft"] = True
        self.assertIn("PR: Draft", self.render()["footer"]["text"])
        self.payload["_discord_pr_status"] = {
            "state": "MERGED",
            "reviewDecision": "APPROVED",
        }
        self.assertEqual(
            "PR: Merged · Review: Approved", self.render()["footer"]["text"]
        )
        self.payload.pop("_discord_pr_status")
        self.assertIn("Review: Unavailable", self.render()["footer"]["text"])

    def test_issue_uses_its_own_icons_and_no_checks(self):
        self.payload["issue"] = self.payload.pop("pull_request")
        for action, emoji, color in (
            ("opened", "<:issueopened:896502343288381521>", 0x6CC644),
            ("closed", "<:issueclosed:896502343565185031>", 0xFF4444),
        ):
            self.payload["action"] = action
            embed = self.render("issues")
            self.assertEqual(emoji + " A **formatted** title", embed["title"])
            self.assertEqual(color, embed["color"])
            self.assertNotIn("fields", embed)

    def test_empty_review_and_separate_red_inline_comment(self):
        self.payload["action"] = "submitted"
        self.payload["review"] = {
            "state": "COMMENTED",
            "body": "",
            "user": self.payload["sender"],
        }
        self.assertIsNone(
            format_event("pull_request_review", self.payload, self.config)[0]
        )
        for state in ("APPROVED", "CHANGES_REQUESTED"):
            self.payload["review"]["state"] = state
            self.assertIsNotNone(
                format_event("pull_request_review", self.payload, self.config)[
                    0
                ]
            )
        self.payload["action"] = "created"
        self.payload["comment"] = {
            "body": "оно в прототипе ног проставляется",
            "user": {"login": "banumbas"},
            "path": "Resources/Prototypes/demon.yml",
            "original_line": 99,
            "pull_request_review_id": 123,
        }
        self.payload["_discord_review_state"] = "commented"
        embed = self.render("pull_request_review_comment")
        self.assertEqual("banumbas", embed["author"]["name"])
        self.assertEqual(0xFF4444, embed["color"])
        self.assertTrue(
            embed["title"].startswith("👁 Review comment · Comment")
        )
        self.assertEqual(self.payload["comment"]["body"], embed["description"])
        self.assertIn("demon.yml:99", embed["fields"][0]["value"])
        self.assertNotIn("thumbnail", embed)
        self.payload["comment"]["user"] = {"login": "Kinar7"}
        self.payload["comment"]["body"] = "Ответ автора"
        self.assertEqual(
            "Kinar7",
            self.render("pull_request_review_comment")["author"]["name"],
        )

    def test_inline_review_status_comes_from_its_own_review(self):
        self.payload["comment"] = {"pull_request_review_id": 123}
        github = Mock(root="/repos/owner/repo")
        github.pull_status.return_value = self.payload["_discord_pr_status"]
        github.json.return_value = {"state": "CHANGES_REQUESTED"}
        enrich_event(
            "pull_request_review_comment",
            self.payload,
            github,
            self.config,
            Mock(),
        )
        github.json.assert_called_once_with(
            "GET", "/repos/owner/repo/pulls/1/reviews/123"
        )
        self.assertEqual(
            "changes_requested", self.payload["_discord_review_state"]
        )
        github.json.side_effect = GitHubError("HTTP 503")
        enrich_event(
            "pull_request_review_comment",
            self.payload,
            github,
            self.config,
            Mock(),
        )
        self.assertNotIn("_discord_review_state", self.payload)
        self.config["icons"]["approved"] = "<:gh_approved:123456789>"
        self.payload["review"] = {
            "state": "APPROVED",
            "user": {"login": "Reviewer"},
        }
        self.payload.pop("comment")
        embed = self.render("pull_request_review")
        self.assertTrue(
            embed["title"].startswith("<:gh_approved:123456789> Approved")
        )
        self.assertEqual("Reviewer", embed["author"]["name"])

    def test_comment_uses_github_heading_and_status_footer(self):
        self.payload["action"] = "created"
        self.payload["issue"] = self.payload.pop("pull_request")
        self.payload["issue"]["pull_request"] = {"url": "api-url"}
        self.payload["comment"] = {
            "body": "A **comment**",
            "user": self.payload["sender"],
            "html_url": "https://github.com/owner/repo/pull/1#issuecomment-3",
        }
        embed = self.render("issue_comment")
        self.assertEqual(
            "💬 [repo] New comment on pull request #1: A **formatted** title",
            embed["title"],
        )
        self.assertEqual("A **comment**", embed["description"])
        self.assertEqual(
            "PR: Open · Review: Waiting for review", embed["footer"]["text"]
        )
        self.assertNotIn("color", embed)
        self.assertNotIn("fields", embed)
        self.assertIn("author", embed)
        self.payload["comment"]["user"] = {"login": "Commenter"}
        self.assertEqual(
            "Commenter", self.render("issue_comment")["author"]["name"]
        )

    def test_description_is_not_cut_at_old_mommi_limit(self):
        self.payload["pull_request"]["body"] = "x" * 501
        self.payload["pull_request"]["reactions"] = {}
        self.assertEqual("x" * 501 + "\n\u200b", self.render()["description"])

    def test_push_matches_mommi(self):
        self.payload["ref"] = "refs/heads/master"
        self.payload["commits"] = [
            {
                "id": "a" * 40,
                "message": "Short message",
                "author": {"name": "Human"},
                "url": "https://github.com/owner/repo/commit/" + "a" * 40,
            }
        ]
        self.payload["compare"] = (
            "https://github.com/owner/repo/compare/old...new"
        )
        embed = self.render("push")
        self.assertEqual(
            "**1** New Commit to **refs/heads/master**", embed["title"]
        )
        self.assertEqual(
            "[`aaaaaaa`](https://github.com/owner/repo/commit/"
            + "a" * 40
            + ") Short message",
            embed["description"],
        )
        self.assertNotIn("footer", embed)
        self.assertNotIn("color", embed)
        self.payload["forced"] = True
        forced = self.render("push")
        self.assertEqual(0xFF0000, forced["color"])
        self.assertEqual("[FORCE PUSHED] " + embed["title"], forced["title"])

    def test_push_truncates_at_eleven_commits_and_67_characters(self):
        self.payload["commits"] = [
            {"id": "a" * 40, "message": "x" * 100, "author": {"name": "Human"}}
            for _ in range(12)
        ]
        embed = self.render("push")
        self.assertEqual(11, embed["description"].count("[`aaaaaaa`]"))
        self.assertIn("x" * 67 + "...", embed["description"])
        self.assertTrue(embed["description"].endswith("<.....>"))

    def test_discussion_lifecycle_matches_mommi(self):
        self.payload["discussion"] = self.payload.pop("pull_request")
        for action, label, color in (
            ("created", "Created", None),
            ("answered", "Answered", 0x6CC644),
            ("unanswered", "Unanswered", 0xFF4444),
        ):
            self.payload["action"] = action
            embed = self.render("discussion")
            self.assertEqual(
                f"<:ghdiscussion:1082078543770554429> {label}: "
                "A **formatted** title",
                embed["title"],
            )
            self.assertEqual(color, embed.get("color"))
            self.assertEqual(action == "created", "author" in embed)
            self.assertEqual(
                "**Description**\nSecond line", embed["description"]
            )

    def test_disallowed_avatar_is_not_embedded(self):
        self.payload["sender"]["avatar_url"] = "http://127.0.0.1/private"
        self.assertNotIn("icon_url", self.render()["author"])

    def test_large_checks_fit_without_losing_whole_notification(self):
        self.payload["_discord_checks"] *= 100
        message = format_event("pull_request", self.payload, self.config)[0]
        self.assertEqual(2, len(message["embeds"]))
        self.assertNotIn("_files", message)
        description = message["embeds"][1]["description"]
        self.assertEqual(100, description.count("✓ Build"))
        self.assertEqual(100, description.count("⏳ Tests"))
        self.config["icons"]["approved"] = "<:gh_approved:1234567890123456789>"
        message = format_event("pull_request", self.payload, self.config)[0]
        descriptions = [
            embed["description"] for embed in message["embeds"][1:]
        ]
        self.assertTrue(all(units(value) <= 4096 for value in descriptions))
        self.assertEqual(100, "".join(descriptions).count("Build"))
        self.assertTrue(
            all(len(part["embeds"]) <= 10 for part in split_message(message))
        )

    def test_read_only_metadata_and_partial_failure(self):
        github = Mock(root="/repos/owner/repo")
        github.pages.side_effect = [
            [{"content": "+1"}, {"content": "+1"}, {"content": "-1"}],
            [{"name": "Build", "status": "queued"}],
            [],
        ]
        github.json.return_value = {"mergeable": True}
        github.pull_status.return_value = self.payload["_discord_pr_status"]
        enrich_event("pull_request", self.payload, github, self.config, Mock())
        self.assertEqual(
            {"+1": 2, "-1": 1}, self.payload["pull_request"]["reactions"]
        )
        self.assertTrue(self.payload["pull_request"]["mergeable"])
        self.assertTrue(
            all(call.args[0] == "GET" for call in github.json.call_args_list)
        )
        github.json.side_effect = GitHubError("HTTP 403")
        github.pages.side_effect = GitHubError("HTTP 403")
        saved = copy.deepcopy(self.payload)
        log = Mock()
        enrich_event("pull_request", self.payload, github, self.config, log)
        self.assertEqual(saved, self.payload)
        self.assertEqual(3, log.call_count)

    def test_complete_markdown_images_and_discord_limits(self):
        screenshot = "https://github.com/user-attachments/assets/example"
        second = "https://example.org/picture(test).png"
        body = "# Описание\n\n" + "Строка **текста** 😀\n" * 500
        body += f'![Снимок]({screenshot})\n<img src="{second}">\n'
        body += "```python\n" + "print('проверка')\n" * 400 + "```\nКонец"
        self.payload["pull_request"]["body"] = body
        message, _ = format_event("pull_request", self.payload, self.config)
        images = [
            embed["image"]["url"]
            for embed in message["embeds"]
            if "image" in embed
            and not embed["image"]["url"].startswith("attachment:")
        ]
        self.assertEqual([screenshot, second], images)
        visible = "".join(
            embed.get("description", "") for embed in message["embeds"]
        )
        self.assertEqual(500, visible.count("Строка **текста** 😀"))
        self.assertEqual(400, visible.count("print('проверка')"))
        self.assertIn("Конец", visible)
        for part in split_message(message):
            self.assertLessEqual(len(part["embeds"]), 10)
            size = 0
            for embed in part["embeds"]:
                self.assertLessEqual(units(embed.get("description", "")), 4096)
                size += sum(
                    units(embed.get(key, ""))
                    for key in ("title", "description")
                )
                size += units(embed.get("author", {}).get("name", ""))
                size += sum(
                    units(field["name"]) + units(field["value"])
                    for field in embed.get("fields", [])
                )
            self.assertLessEqual(size, 6000)
        plain_body = "😀" * 10000
        self.assertEqual(plain_body, "".join(split_body(plain_body, 3500)))
        visible, _ = prepare_body(
            "<!-- hidden -->\n```html\n<!-- visible code -->\n```",
            "https://github.com/owner/repo/blob/main/",
        )
        self.assertNotIn("hidden", visible)
        self.assertIn("<!-- visible code -->", visible)
        fenced = "```python\n" + "x" * 3400 + "\n```"
        self.assertTrue(
            all(units(part) <= 3500 for part in split_body(fenced, 3500))
        )
        _, images = prepare_body(
            "![test][image]\n[image]: image.png\n"
            "`![not an image](https://example.org/code.png)`\n"
            '<img src="http://127.0.0.1/a"><img src="https://localhost/a">',
            "https://github.com/owner/repo/blob/main/",
        )
        self.assertEqual(
            ["https://raw.githubusercontent.com/owner/repo/main/image.png"],
            images,
        )

    def test_actual_head_merge_checks_and_latest_legacy_status(self):
        github = Mock(root="/repos/owner/repo")
        self.config["display"]["show_reactions"] = False
        github.json.return_value = {
            "head": {"sha": "a" * 40},
            "state": "open",
            "merge_commit_sha": "b" * 40,
        }
        github.pages.side_effect = [
            [{"id": 1, "name": "Lint", "status": "queued"}],
            [
                {
                    "id": 2,
                    "name": "Lint",
                    "status": "completed",
                    "conclusion": "success",
                },
                {
                    "id": 3,
                    "name": "Server integration",
                    "status": "in_progress",
                },
            ],
            [
                {
                    "context": "External test",
                    "state": "failure",
                    "target_url": "",
                },
                {
                    "context": "External test",
                    "state": "pending",
                    "target_url": "",
                },
            ],
        ]
        enrich_event("pull_request", self.payload, github, self.config, Mock())
        checks = self.payload["_discord_checks"]
        self.assertEqual(
            ["Lint", "Server integration", "External test"],
            [check["name"] for check in checks],
        )
        self.assertEqual("success", checks[0]["conclusion"])
        self.assertEqual("failure", checks[-1]["conclusion"])
        self.assertIn("b" * 40, github.pages.call_args_list[1].args[0])


if __name__ == "__main__":
    unittest.main()
