"""Контракт карточек из основной ветки v2 MoMMI, без его зависимостей."""

import copy
import unittest
from unittest.mock import Mock

from discord_notifications.config import load_config
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

    def render(self, event="pull_request"):
        return format_event(event, self.payload, self.config)[0]["embeds"][0]

    def test_pr_matches_mommi_card(self):
        self.assertEqual(
            {
                "title": "<:propen:896496081960054827> A **formatted** title",
                "url": "https://github.com/owner/repo/pull/1",
                "color": 0x6CC644,
                "description": (
                    "**Description**\nSecond line\n"
                    "<:upvote:590257887826411590> 3   "
                    "<:downvote:590257835447812207> 1\u200b"
                ),
                "author": {
                    "name": "Human",
                    "url": "https://github.com/Human",
                    "icon_url": "https://avatars.githubusercontent.com/u/1",
                },
                "footer": {
                    "text": "owner/repo#1 by Author",
                    "icon_url": "https://avatars.githubusercontent.com/u/2",
                },
                "fields": [
                    {
                        "name": "Checks",
                        "value": "`Build 😄`\n`Tests 🏃`\n",
                        "inline": True,
                    },
                    {
                        "name": "status",
                        "value": "🚨CONFLICTS🚨",
                        "inline": True,
                    },
                ],
            },
            self.render(),
        )

    def test_closed_and_merged_icons_and_palette(self):
        self.payload["action"] = "closed"
        for merged, color, emoji in (
            (False, 0xFF4444, "<:prclosed:896496081976827986>"),
            (True, 0x6E5494, "<:prmerge:896496082324979772>"),
        ):
            with self.subTest(merged=merged):
                self.payload["pull_request"]["merged"] = merged
                embed = self.render()
                self.assertEqual(color, embed["color"])
                self.assertEqual(
                    emoji + " A **formatted** title", embed["title"]
                )

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

    def test_comment_matches_mommi_without_color_or_footer_avatar(self):
        self.payload["action"] = "created"
        self.payload["issue"] = self.payload.pop("pull_request")
        self.payload["comment"] = {
            "body": "A **comment**",
            "user": self.payload["sender"],
            "html_url": "https://github.com/owner/repo/pull/1#issuecomment-3",
        }
        embed = self.render("issue_comment")
        self.assertEqual("New Comment: A **formatted** title", embed["title"])
        self.assertEqual("A **comment**", embed["description"])
        self.assertEqual({"text": "owner/repo#1 by Author"}, embed["footer"])
        self.assertNotIn("color", embed)
        self.assertNotIn("fields", embed)
        self.assertIn("author", embed)

    def test_description_limit_and_invisible_tail(self):
        self.payload["pull_request"]["body"] = "x" * 501
        self.payload["pull_request"]["reactions"] = {}
        self.assertEqual(
            "x" * 500 + "...\n\u200b", self.render()["description"]
        )

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
            + ") Short message\n",
            embed["description"],
        )
        self.assertEqual({"text": "owner/repo"}, embed["footer"])
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
                "**Description**\nSecond line\n", embed["description"]
            )

    def test_disallowed_avatar_is_not_embedded(self):
        self.payload["sender"]["avatar_url"] = "http://127.0.0.1/private"
        self.assertNotIn("icon_url", self.render()["author"])

    def test_large_checks_fit_without_losing_whole_notification(self):
        self.payload["_discord_checks"] *= 100
        field = self.render()["fields"][0]
        self.assertLessEqual(
            len(field["value"].encode("utf-16-le")) // 2, 1024
        )
        self.assertTrue(field["value"].endswith("<.....>"))

    def test_read_only_metadata_and_partial_failure(self):
        github = Mock(root="/repos/owner/repo")
        github.json.side_effect = [
            [{"content": "+1"}, {"content": "+1"}, {"content": "-1"}],
            {"mergeable": True},
            {"check_runs": [{"name": "Build", "status": "queued"}]},
        ]
        enrich_event("pull_request", self.payload, github, self.config, Mock())
        self.assertEqual(
            {"+1": 2, "-1": 1}, self.payload["pull_request"]["reactions"]
        )
        self.assertTrue(self.payload["pull_request"]["mergeable"])
        self.assertTrue(
            all(call.args[0] == "GET" for call in github.json.call_args_list)
        )
        github.json.side_effect = GitHubError("HTTP 403")
        saved = copy.deepcopy(self.payload)
        log = Mock()
        enrich_event("pull_request", self.payload, github, self.config, log)
        self.assertEqual(saved, self.payload)
        self.assertEqual(2, log.call_count)


if __name__ == "__main__":
    unittest.main()
