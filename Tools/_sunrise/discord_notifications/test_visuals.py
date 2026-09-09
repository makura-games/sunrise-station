"""Контракт Components V2: оформление, полный текст и порядок изображений."""

import copy
import json
import unittest
from unittest.mock import Mock

from discord_notifications.config import load_config
from discord_notifications.content import (
    prepare_body,
    split_body,
    split_message,
    units,
    walk_components,
)
from discord_notifications.formatting import format_event
from discord_notifications.github import GitHubError
from discord_notifications.metadata import enrich_event
from discord_notifications.test_notifications import (
    CONFIG,
    event_payload,
    message_text,
)


class NotificationVisualTests(unittest.TestCase):
    def setUp(self):
        self.config = load_config(CONFIG)
        self.payload = event_payload()
        self.payload["pull_request"].update(
            {
                "title": "Изменение интерфейса",
                "body": "**Описание**<!-- hidden -->\nПродолжение",
                "state": "open",
                "reactions": {"+1": 3, "-1": 1},
            }
        )
        self.payload["pull_request"]["user"].update(
            {
                "avatar_url": "https://avatars.githubusercontent.com/u/2",
                "html_url": "https://github.com/Author",
            }
        )
        self.payload["_discord_pr_status"] = {
            "state": "OPEN",
            "isDraft": False,
            "reviewDecision": "REVIEW_REQUIRED",
        }

    def render(self, event="pull_request"):
        return format_event(event, self.payload, self.config)[0]

    def test_pr_uses_github_identity_author_link_color_and_original_body(self):
        message = self.render()
        self.assertEqual("GitHub", message["username"])
        self.assertEqual(
            "https://avatars.githubusercontent.com/u/9919?v=4",
            message["avatar_url"],
        )
        self.assertEqual(32768, message["flags"])
        self.assertNotIn("embeds", message)
        self.assertNotIn("content", message)
        container = message["components"][0]
        self.assertEqual(17, container["type"])
        self.assertEqual(0x6CC644, container["accent_color"])
        header = container["components"][0]
        self.assertEqual(9, header["type"])
        self.assertEqual(
            "-# [Author](<https://github.com/Author>)",
            header["components"][0]["content"],
        )
        self.assertEqual(
            {
                "type": 11,
                "media": {
                    "url": "https://avatars.githubusercontent.com/u/2",
                },
            },
            header["accessory"],
        )
        self.assertEqual(
            "**[<:propen:896496081960054827> Изменение интерфейса]"
            "(<https://github.com/owner/repo/pull/1>)**",
            header["components"][1]["content"],
        )
        text = message_text(message)
        self.assertIn("**Описание**\nПродолжение\n＋ 3   − 1", text)
        self.assertNotIn("hidden", text)
        self.assertTrue(text.endswith("PR: Open · Review: Waiting for review"))

    def test_closed_merged_and_review_palette_with_correct_authors(self):
        self.payload["action"] = "closed"
        for merged, color, icon in (
            (False, 0xFF4444, "<:prclosed:896496081976827986>"),
            (True, 0x6E5494, "<:prmerge:896496082324979772>"),
        ):
            self.payload["pull_request"]["merged"] = merged
            message = self.render()
            self.assertEqual(color, message["components"][0]["accent_color"])
            self.assertIn(icon, message_text(message))
            self.assertIn("[Author]", message_text(message))
            self.assertNotIn("Human", message_text(message))
        self.payload["action"] = "submitted"
        for state in ("approved", "changes_requested", "commented"):
            self.payload["review"] = {
                "state": state,
                "body": "Текст ревью",
                "user": {
                    "login": "Reviewer",
                    "html_url": "https://github.com/Reviewer",
                },
            }
            message = self.render("pull_request_review")
            style = self.config["reviews"][state]
            self.assertEqual(
                int(style["color"][1:], 16),
                message["components"][0]["accent_color"],
            )
            text = message_text(message)
            self.assertIn(
                self.config["icons"][style["icon"]] + " " + style["label"],
                text,
            )
            self.assertIn("-# [Reviewer]", text)
            self.assertEqual(1, text.count("[Reviewer]"))
            self.assertNotIn("[Author]", text)
            self.assertIn("Текст ревью", text)

    def test_status_and_conflicts_only_at_bottom(self):
        for state, draft, decision, expected in (
            (
                "OPEN",
                False,
                "REVIEW_REQUIRED",
                "Open · Review: Waiting for review",
            ),
            ("OPEN", True, None, "Draft · Review: No review decision"),
            ("MERGED", False, "APPROVED", "Merged · Review: Approved"),
            (
                "CLOSED",
                False,
                "CHANGES_REQUESTED",
                "Closed · Review: Changes requested",
            ),
        ):
            self.payload["_discord_pr_status"] = {
                "state": state,
                "isDraft": draft,
                "reviewDecision": decision,
            }
            self.assertTrue(message_text(self.render()).endswith(expected))
        for mergeable in ("CONFLICTING", "MERGEABLE", "UNKNOWN", None):
            self.payload["_discord_pr_status"]["mergeable"] = mergeable
            self.assertEqual(
                mergeable == "CONFLICTING",
                "Merge conflicts" in message_text(self.render()),
            )

    def test_comments_have_own_author_and_inline_reviews_stay_red(self):
        self.payload["action"] = "created"
        self.payload["comment"] = {
            "user": {"login": "Commenter"},
            "body": "Замечание к строке",
            "path": "Resources/Prototypes/demon.yml",
            "original_line": 99,
        }
        self.payload["_discord_review_state"] = "commented"
        message = self.render("pull_request_review_comment")
        text = message_text(message)
        self.assertIn("-# Commenter", text)
        self.assertIn(
            self.config["icons"]["review"] + " Review comment · Comment", text
        )
        self.assertIn("Замечание к строке", text)
        self.assertIn("demon.yml:99", text)
        self.assertEqual(0xFF4444, message["components"][0]["accent_color"])
        message = self.render("issue_comment")
        self.assertIn(self.config["icons"]["comment"], message_text(message))
        self.assertIsNone(message["components"][0]["accent_color"])

    def test_empty_commented_review_is_not_an_empty_card(self):
        self.payload["review"] = {
            "state": "COMMENTED",
            "body": "",
            "user": {"login": "Human"},
        }
        self.assertIsNone(self.render("pull_request_review"))
        for state in ("APPROVED", "CHANGES_REQUESTED"):
            self.payload["review"]["state"] = state
            self.assertIsNotNone(self.render("pull_request_review"))

    def test_mixed_image_syntax_and_duplicate_urls_keep_original_positions(
        self,
    ):
        first = "https://example.org/one.png"
        second = "https://example.org/two(test).png"
        blocks = prepare_body(
            f'A\n![Первый]({first})\nB\n<img src="{second}" width="500">\n'
            f"C\n![Повтор][ref]\nD\n[ref]: {first}\n",
            "",
        )
        self.assertEqual(
            [10, 12, 10, 12, 10, 12, 10], [block["type"] for block in blocks]
        )
        self.assertEqual(
            ["A", "B", "C", "D"],
            [block["content"] for block in blocks if block["type"] == 10],
        )
        self.assertEqual(
            [first, second, first],
            [
                block["items"][0]["media"]["url"]
                for block in blocks
                if block["type"] == 12
            ],
        )

    def test_linked_image_preserves_picture_and_target(self):
        blocks = prepare_body(
            "[![Снимок](https://example.org/a.png)](https://example.org/page)"
            "\nТекст после",
            "",
        )
        self.assertEqual([12, 10], [block["type"] for block in blocks])
        self.assertEqual(
            "https://example.org/a.png", blocks[0]["items"][0]["media"]["url"]
        )
        self.assertIn("https://example.org/page", blocks[1]["content"])
        self.assertIn("Текст после", blocks[1]["content"])

    def test_code_escapes_and_hidden_comments_are_not_images(self):
        body = (
            "<!-- ![hidden](https://example.org/hidden.png) -->\n"
            "`![inline](https://example.org/code.png)`\n"
            "````markdown\n```\n![code](https://example.org/code.png)\n```\n````\n"
            "\\![escaped](https://example.org/escaped.png)\n"
            "![real](images/test.png)"
        )
        blocks = prepare_body(body, "https://github.com/owner/repo/blob/main/")
        images = [block for block in blocks if block["type"] == 12]
        self.assertEqual(1, len(images))
        self.assertEqual(
            "https://raw.githubusercontent.com/owner/repo/main/images/test.png",
            images[0]["items"][0]["media"]["url"],
        )
        visible = "\n".join(block.get("content", "") for block in blocks)
        self.assertNotIn("hidden", visible)
        self.assertIn("![inline]", visible)
        self.assertIn("![code]", visible)
        self.assertIn("\\![escaped]", visible)

    def test_hidden_template_fences_do_not_hide_following_images(self):
        blocks = prepare_body(
            "До\n<!-- hidden template\n```markdown\nexample\n```\n-->\n"
            "![real](https://example.org/real.png)\nПосле\n"
            "```html\n<!-- visible code -->\n```",
            "",
        )
        text = "".join(block.get("content", "") for block in blocks)
        self.assertNotIn("hidden template", text)
        self.assertNotIn("example", text)
        self.assertIn("<!-- visible code -->", text)
        self.assertEqual([10, 12, 10], [block["type"] for block in blocks])

    def test_long_unicode_and_many_images_fit_without_reordering(self):
        body = "# Описание\n" + "Строка **текста** 😀\n" * 500
        for number in range(65):
            body += f"Перед {number}\n![{number}](https://example.org/{number}.png)\n"
        body += "```python\n" + "print('проверка')\n" * 400 + "```\nКонец"
        self.payload["pull_request"]["body"] = body
        parts = split_message(self.render())
        self.assertGreater(len(parts), 2)
        text = "".join(message_text(part) for part in parts)
        self.assertEqual(500, text.count("Строка **текста** 😀"))
        self.assertEqual(400, text.count("print('проверка')"))
        self.assertIn("Конец", text)
        images = []
        for part in parts:
            nodes = list(walk_components(part["components"]))
            self.assertLessEqual(len(nodes), 40)
            self.assertLessEqual(
                sum(units(node.get("content", "")) for node in nodes), 4000
            )
            images.extend(
                node["items"][0]["media"]["url"]
                for node in nodes
                if node["type"] == 12
            )
        self.assertEqual(
            [f"https://example.org/{number}.png" for number in range(65)],
            images,
        )
        self.payload["pull_request"]["body"] = "界" * 7000
        parts = split_message(self.render())
        self.assertEqual(
            7000, sum(message_text(part).count("界") for part in parts)
        )
        self.assertTrue(
            all(
                len(
                    json.dumps(
                        part["components"][0], ensure_ascii=False
                    ).encode()
                )
                <= 9000
                for part in parts
            )
        )
        plain = "😀" * 10000
        self.assertEqual(plain, "".join(split_body(plain, 3500)))

    def test_footer_stays_with_last_text_instead_of_a_separate_message(self):
        for length in (3600, 3700, 3800, 3900, 4000):
            self.payload["pull_request"]["body"] = "x" * length
            parts = split_message(self.render())
            self.assertTrue(
                message_text(parts[-1]).startswith("x") or len(parts) == 1
            )
            self.assertIn("PR: Open", message_text(parts[-1]))
            self.assertEqual(
                length, sum(message_text(part).count("x") for part in parts)
            )

    def test_invalid_media_and_avatar_are_not_requested(self):
        self.payload["pull_request"]["body"] = (
            '<img src="http://127.0.0.1/a"><img src="https://localhost/a">'
        )
        self.payload["pull_request"]["user"]["avatar_url"] = "http://127.0.0.1"
        self.assertFalse(
            any(
                node["type"] in {11, 12}
                for node in walk_components(self.render()["components"])
            )
        )

    def test_metadata_uses_parent_review_and_preserves_reactions_on_error(
        self,
    ):
        github = Mock(root="/repos/owner/repo")
        github.pull_status.return_value = self.payload["_discord_pr_status"]
        github.json.return_value = {"state": "CHANGES_REQUESTED"}
        self.payload["comment"] = {"pull_request_review_id": 123}
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
        github.pages.return_value = [{"content": "+1"}, {"content": "-1"}]
        enrich_event("pull_request", self.payload, github, self.config, Mock())
        self.assertEqual(
            {"+1": 1, "-1": 1}, self.payload["pull_request"]["reactions"]
        )
        saved = copy.deepcopy(self.payload)
        github.pages.side_effect = GitHubError("HTTP 403")
        enrich_event("pull_request", self.payload, github, self.config, Mock())
        self.assertEqual(saved, self.payload)

    def test_push_and_discussion_keep_content_and_colors(self):
        self.payload["commits"] = [
            {"id": "a" * 40, "message": "x" * 100, "author": {"name": "Human"}}
            for _ in range(12)
        ]
        message = self.render("push")
        self.assertEqual(11, message_text(message).count("[`aaaaaaa`]"))
        self.assertIn("x" * 67 + "...", message_text(message))
        self.payload["forced"] = True
        self.assertEqual(
            0xFF0000, self.render("push")["components"][0]["accent_color"]
        )
        self.payload["discussion"] = self.payload.pop("pull_request")
        self.payload["action"] = "answered"
        message = self.render("discussion")
        self.assertIn("Answered", message_text(message))
        self.assertNotIn("-# Human", message_text(message))
        self.assertEqual(0x6CC644, message["components"][0]["accent_color"])


if __name__ == "__main__":
    unittest.main()
