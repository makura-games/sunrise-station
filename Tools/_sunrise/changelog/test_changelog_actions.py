import base64
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from datetime import datetime, timezone
from pathlib import Path
from unittest.mock import Mock, call, patch

import yaml

os.environ["CHANGELOG_FILE"] = "Resources/Changelog/ChangelogSunrise.yml"

import actions_changelogs_since_last_run as discord_changelog
import changelog_actions
import changelog_schema
import changelog_targets
import dispatch_changelogs
import manual_changelog

discord_changelog.DISCORD_WEBHOOK_URL = (
    "https://discord.com/api/webhooks/123/test-token"
)


REPO_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPO_ROOT / ".github/workflows/sunrise-changelog.yml"
PUBLISH_WORKFLOW_PATH = REPO_ROOT / ".github/workflows/sunrise-publish-stable.yml"
DISCORD_WORKFLOW_PATH = REPO_ROOT / ".github/workflows/sunrise-publish-discord-changelog.yml"
DISPATCH_WORKFLOW_PATH = REPO_ROOT / ".github/workflows/sunrise-dispatch-discord-changelogs.yml"
VALIDATE_WORKFLOW_PATH = REPO_ROOT / ".github/workflows/sunrise-validate-changelog.yml"
RUNNER_PATH = REPO_ROOT / "Tools/_sunrise/changelog/run.sh"


class ChangelogActionsTests(unittest.TestCase):
    def test_rollback_changelog_uses_selected_sha(self):
        selected_sha = "a" * 40
        workflow_sha = "b" * 40
        run = {
            "display_title": f"Publish Stable {selected_sha.upper()}",
            "head_commit": {"id": workflow_sha},
        }
        self.assertEqual(selected_sha, dispatch_changelogs.published_run_sha(run))
        with patch.dict(os.environ, {"RELEASED_SHA": ""}), patch.object(
            dispatch_changelogs, "github_request", return_value=run
        ):
            self.assertEqual(selected_sha, dispatch_changelogs.resolve_released_sha("org/repo", "token", "123"))

        self.assertEqual(workflow_sha, dispatch_changelogs.published_run_sha({"head_commit": run["head_commit"]}))
        self.assertIsNone(dispatch_changelogs.published_run_sha({"head_commit": None}))

    def test_changelog_file_must_stay_inside_repository(self):
        for invalid_path in (
            "../outside.yml",
            "/outside.yml",
            "C:/outside.yml",
            "C:outside.yml",
            "\\outside.yml",
        ):
            with self.subTest(invalid_path=invalid_path), patch.dict(
                "os.environ", {"CHANGELOG_FILE": invalid_path}
            ):
                with self.assertRaisesRegex(RuntimeError, "относительным путём внутри репозитория"):
                    changelog_actions.configured_changelog_file()

        with patch.dict("os.environ", {"CHANGELOG_FILE": "Changelog.yml"}):
            with self.assertRaisesRegex(RuntimeError, "родительский каталог"):
                changelog_actions.configured_changelog_file()

    def test_legacy_non_padded_changelog_time_is_supported(self):
        self.assertEqual(
            datetime(2024, 1, 4, 1, 30, tzinfo=timezone.utc),
            changelog_actions.parse_time("2024-1-4T01:30:00.0000000+00:00"),
        )

    def test_parser_matches_upstream_syntax(self):
        body = """
            <!-- :cl: из шаблона не должен учитываться -->
            :cl: Ev1__l P-JB2323, Other & Co
            - add: Added a thing
            * bugfix: Fixed a thing
            ADMIN:
            tweak: Tweaked a thing
            MAIN:
            bug: Fixed another thing
        """

        author, categories = changelog_actions.parse_pr_body(body, "Fallback", ("Main", "Admin"))

        self.assertEqual("Ev1__l P-JB2323, Other & Co", author)
        self.assertEqual(
            [
                changelog_actions.ParsedCategory(
                    "Main",
                    [
                        {"type": "Add", "message": "Added a thing"},
                        {"type": "Fix", "message": "Fixed a thing"},
                        {"type": "Fix", "message": "Fixed another thing"},
                    ],
                ),
                changelog_actions.ParsedCategory(
                    "Admin",
                    [{"type": "Tweak", "message": "Tweaked a thing"}],
                ),
            ],
            categories,
        )

    def test_parser_uses_fallback_author_and_keeps_category_after_invalid_directive(self):
        parsed = changelog_actions.parse_pr_body(
            ":cl:\nADMIN:\n- remove: First\nNOTACATEGORY:\n- fix: Second",
            "Fallback",
            ("Main", "Admin"),
        )

        self.assertEqual(
            (
                "Fallback",
                [
                    changelog_actions.ParsedCategory(
                        "Admin",
                        [
                            {"type": "Remove", "message": "First"},
                            {"type": "Fix", "message": "Second"},
                        ],
                    ),
                ],
            ),
            parsed,
        )

    def test_parser_accepts_russian_author_and_text(self):
        parsed = changelog_actions.parse_pr_body(
            ":cl: Иван Иванов\n- add: Добавлена очень важная рыба",
            "Fallback",
        )

        self.assertEqual(
            (
                "Иван Иванов",
                [
                    changelog_actions.ParsedCategory(
                        "Main",
                        [{"type": "Add", "message": "Добавлена очень важная рыба"}],
                    ),
                ],
            ),
            parsed,
        )

    def test_parser_preserves_multiline_change_text(self):
        parsed = changelog_actions.parse_pr_body(
            ":cl: Иван\n"
            "- add: Первая строка\n"
            "Вторая строка\n"
            "  Третья строка\n"
            "- fix: Другая запись",
            "Fallback",
        )

        self.assertEqual(
            "Первая строка\nВторая строка\nТретья строка",
            parsed[1][0].changes[0]["message"],
        )
        self.assertEqual("Другая запись", parsed[1][0].changes[1]["message"])

    def test_comments_with_greater_than_are_removed(self):
        body = ":cl: Иван\n- add: Начало <!-- 2 > 1 --> конец"

        parsed = changelog_actions.parse_pr_body(body, "Fallback")

        self.assertEqual("Начало  конец", parsed[1][0].changes[0]["message"])
        self.assertTrue(
            changelog_actions.is_changelog_template(
                body,
                ":cl: Иван\n- add: Начало  конец",
            ),
        )
        self.assertIsNone(changelog_actions.COMMENT_RE.search(r"\<!-- не комментарий -->"))
        self.assertEqual(
            r"<!-- первый \--> настоящий конец -->",
            changelog_actions.COMMENT_RE.search(
                r"<!-- первый \--> настоящий конец -->",
            ).group(),
        )

    def test_parser_ignores_changelog_inside_fenced_code(self):
        for fence in ("```md", "~~~text"):
            with self.subTest(fence=fence):
                body = (
                    f"{fence}\r\n"
                    ":cl: Пример\r\n"
                    "- add: Не настоящий чейнджлог\r\n"
                    f"{fence[:3]}\r\n"
                    "\r\n"
                    ":cl: Настоящий автор\r\n"
                    "- fix: Настоящее исправление\r\n"
                    ":end-cl:\r\n"
                )

                parsed = changelog_actions.parse_pr_body(body, "Fallback")

                self.assertEqual("Настоящий автор", parsed[0])
                self.assertEqual(
                    [{"type": "Fix", "message": "Настоящее исправление"}],
                    parsed[1][0].changes,
                )

        self.assertIsNone(
            changelog_actions.parse_pr_body(
                "```md\n:cl: Только пример\n- add: Не публиковать\n```",
                "Fallback",
            ),
        )

    def test_template_check_ignores_fenced_example_and_text_after_boundary(self):
        template = ":cl: ВАШЕ_ИМЯ\n- add: ТЕКСТ\n:end-cl:"
        body = (
            "```md\n:cl: Пример\n- add: Не настоящий чейнджлог\n```\n"
            ":cl: ВАШЕ_ИМЯ\n"
            "<!-- Комментарии при сравнении не учитываются. -->\n"
            "- add: ТЕКСТ\n"
            ":end-cl:\n"
            "## Summary by CodeRabbit\n"
            "Этот текст находится за границей."
        )

        self.assertTrue(changelog_actions.is_changelog_template(body, template))

    def test_parser_stops_at_explicit_or_double_blank_boundary(self):
        for body in (
            ":cl: Иван\n- add: Нужная запись\n:end-cl:\n- fix: Не чейнжлог",
            ":cl: Иван\n- add: Нужная запись\n\n\n- fix: Не чейнжлог",
        ):
            with self.subTest(body=body):
                parsed = changelog_actions.parse_pr_body(body, "Fallback")
                self.assertEqual(
                    [{"type": "Add", "message": "Нужная запись"}],
                    parsed[1][0].changes,
                )

    def test_parser_keeps_legacy_body_without_boundary_and_ignores_comment_spacing(self):
        parsed = changelog_actions.parse_pr_body(
            ":cl: Иван\n"
            "<!-- Комментарий можно оставить. -->\n"
            "- add: Первая запись\n"
            "<!-- Комментарий не считается пустой строкой. -->\n"
            "\n"
            "- fix: Вторая запись",
            "Fallback",
        )

        self.assertEqual(
            [
                {"type": "Add", "message": "Первая запись"},
                {"type": "Fix", "message": "Вторая запись"},
            ],
            parsed[1][0].changes,
        )

    def test_parser_reads_only_media_section_in_authored_order(self):
        parsed = changelog_actions.parse_pr_body(
            "## Медиа (Видео/Скриншоты)\n"
            "Рыба в игре\n"
            "![Встроенная подпись](https://example.org/fish.png)\n"
            "<!-- Служебный комментарий не становится описанием. -->\n"
            "Демонстрация\n"
            "механики\n"
            "<img alt=\"image\" src=\"https://example.org/demo.gif\" />\n"
            "[Видео](https://example.org/demo.webm)\n"
            "### Следующий раздел\n"
            "![Игнорируется](https://example.org/ignored.png)\n"
            ":cl: Иван\n"
            "- add: Добавлена новая рыба\n"
            "media: ![Вне раздела](https://example.org/outside.png)\n"
            "![Тоже вне раздела](https://example.org/outside-2.png)",
            "Fallback",
        )

        self.assertEqual("Иван", parsed[0])
        self.assertEqual(
            [{"type": "Add", "message": "Добавлена новая рыба"}],
            parsed[1][0].changes,
        )
        self.assertEqual([
            {"url": "https://example.org/fish.png", "description": "Рыба в игре"},
            {"url": "https://example.org/demo.gif", "description": "Демонстрация механики"},
            {"url": "https://example.org/demo.webm", "description": "Видео"},
        ], parsed[1][0].media)

    def test_parser_accepts_github_html_image(self):
        parsed = changelog_actions.parse_pr_body(
            "## Медиа (Видео/Скриншоты) | Media (Video/Screenshots)\n"
            "<img width=\"521\" height=\"404\" alt=\"image\" "
            "src=\"https://github.com/user-attachments/assets/ff98fa13-dac1-48cf-bd9d-98e7110f33e6\" />\n"
            ":cl: KaiserMaus\n"
            "- fix: Исправлено ночное зрение вульп.\n"
            ":end-cl:",
            "Fallback",
        )

        self.assertEqual(
            [{
                "url": "https://github.com/user-attachments/assets/ff98fa13-dac1-48cf-bd9d-98e7110f33e6",
                "description": "image",
            }],
            parsed[1][0].media,
        )

    def test_media_section_stops_at_any_heading_or_changelog(self):
        for ending in ("# Раздел", "### Раздел", "###### Раздел", ":cl: Иван"):
            with self.subTest(ending=ending):
                media = changelog_actions.parse_pr_media_section(
                    "## Media\n"
                    "![Первое](https://example.org/first.png)\n"
                    f"{ending}\n"
                    "![Второе](https://example.org/second.png)"
                )
                self.assertEqual(
                    [{"url": "https://example.org/first.png", "description": "Первое"}],
                    media,
                )

        self.assertEqual(
            [],
            changelog_actions.parse_pr_media_section(
                ":cl: Иван\n- add: Запись\n## Медиа\n![Поздно](https://example.org/late.png)"
            ),
        )

    def test_media_marker_supports_translated_templates_and_heading_captions(self):
        translated = changelog_actions.parse_pr_media_section(
            "## Medien und Videos\n"
            "<!-- changelog-media-section -->\n"
            "Beschreibung aus der Vorlage\n"
            "![Bild](https://example.org/image.png)\n"
            "## Nächster Abschnitt"
        )
        self.assertEqual(
            [{"url": "https://example.org/image.png", "description": "Beschreibung aus der Vorlage"}],
            translated,
        )

        for heading in ("## Media — screenshots", "## Media: video", "## Медиа / Скриншоты"):
            with self.subTest(heading=heading):
                self.assertEqual(
                    [{"url": "https://example.org/image.png", "description": "Подпись"}],
                    changelog_actions.parse_pr_media_section(
                        f"{heading}\nПодпись\n![Встроенный alt](https://example.org/image.png)"
                    ),
                )

        self.assertEqual(
            [],
            changelog_actions.parse_pr_media_section(
                "## Medien und Videos\n![Без маркера](https://example.org/image.png)"
            ),
        )

        template = (REPO_ROOT / changelog_actions.PULL_REQUEST_TEMPLATE_PATH).read_text(encoding="utf-8")
        self.assertIn("<!-- changelog-media-section -->", template)

    def test_media_is_ignored_without_changelog_and_outside_media_section(self):
        self.assertIsNone(
            changelog_actions.parse_pr_body(
                "## Медиа\n![Рыба](https://example.org/fish.png)",
                "Fallback",
            ),
        )

        output = io.StringIO()
        with redirect_stdout(output):
            parsed = changelog_actions.parse_pr_body(
                ":cl: Иван\n"
                "- add: Добавлена рыба\n"
                "media: ![Вне раздела](https://example.org/outside.png)\n"
                "![Тоже вне раздела](https://example.org/outside-2.png)",
                "Fallback",
            )
        self.assertEqual([], parsed[1][0].media)
        self.assertEqual("Добавлена рыба", parsed[1][0].changes[0]["message"])
        self.assertIn("Строка media: вне раздела", output.getvalue())

    def test_validation_reports_normalized_changelog(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            event_path = repo_root / "event.json"
            summary_path = repo_root / "summary.md"
            event_path.write_text(
                json.dumps({
                    "pull_request": {
                        "number": 123,
                        "body": (
                            "## Медиа\n"
                            "Рыба\n"
                            "<img alt=\"Старая подпись\" src=\"https://example.org/fish.png\" />\n"
                            ":cl: Иван\n"
                            "- add: Добавлена <рыба>."
                        ),
                        "user": {"login": "Fallback"},
                    },
                }),
                encoding="utf-8",
            )

            with patch.dict(
                "os.environ",
                {
                    "GITHUB_STEP_SUMMARY": str(summary_path),
                    "CHANGELOG_EXTRA_CATEGORIES": "",
                },
            ):
                changelog_actions.validate_pull_request_event(repo_root, event_path)

            summary = summary_path.read_text(encoding="utf-8")

        self.assertIn("### Результат", summary)
        self.assertIn("### Предварительный результат", summary)
        self.assertIn("author: Иван", summary)
        self.assertIn("Добавлена &lt;рыба&gt;.", summary)
        self.assertIn("https://example.org/fish.png", summary)
        self.assertNotIn("Добавлена <рыба>.", summary)

    def test_validation_reports_missing_changelog_as_success(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            event_path = repo_root / "event.json"
            summary_path = repo_root / "summary.md"
            event_path.write_text(
                json.dumps({
                    "pull_request": {
                        "number": 123,
                        "body": "Описание без чейнджлога",
                        "user": {"login": "Tester"},
                    },
                }),
                encoding="utf-8",
            )

            with patch.dict("os.environ", {"GITHUB_STEP_SUMMARY": str(summary_path)}):
                changelog_actions.validate_pull_request_event(repo_root, event_path)

            summary = summary_path.read_text(encoding="utf-8")

        self.assertIn("### Результат", summary)
        self.assertIn("### Что это значит", summary)
        self.assertIn("### Что делать дальше", summary)
        self.assertIn("Блок чейнжлога в описании PR отсутствует", summary)
        self.assertIn("добавьте блок `:cl:` по примеру из шаблона PR", summary)

    def test_validation_reports_parser_error(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            event_path = repo_root / "event.json"
            summary_path = repo_root / "summary.md"
            event_path.write_text(
                json.dumps({
                    "pull_request": {
                        "number": 123,
                        "body": ":cl: Tester\n- add:",
                        "user": {"login": "Tester"},
                    },
                }),
                encoding="utf-8",
            )

            with patch.dict("os.environ", {"GITHUB_STEP_SUMMARY": str(summary_path)}):
                with self.assertRaisesRegex(RuntimeError, "PR #123: чейнджлог не прошёл проверку"):
                    changelog_actions.validate_pull_request_event(repo_root, event_path)

            summary = summary_path.read_text(encoding="utf-8")

        self.assertIn("### Как исправить", summary)
        self.assertIn("не удалось распознать строку чейнжлога", summary)

    def test_manual_parser_requires_ci_author_and_supports_media(self):
        author, categories = changelog_actions.parse_manual_changelog(
            ":ci: Иван Иванов\n"
            "- add: Добавлена рыба\n"
            "media: ![Рыба](https://example.org/fish.png)\n"
            "ADMIN:\n"
            "- fix: Исправлена команда\n"
            "media: https://example.org/demo.webm",
            ("Main", "Admin"),
        )

        self.assertEqual("Иван Иванов", author)
        self.assertEqual(["Main", "Admin"], [category.name for category in categories])
        self.assertEqual("Рыба", categories[0].media[0]["description"])
        self.assertEqual("https://example.org/demo.webm", categories[1].media[0]["url"])

    def test_manual_parser_cleans_author_comment_and_preserves_multiline_text(self):
        author, categories = changelog_actions.parse_manual_changelog(
            ":ci: Иван <!-- служебный комментарий: 2 > 1 -->\n"
            "- add: Первая строка\n"
            "Вторая строка",
        )

        self.assertEqual("Иван", author)
        self.assertEqual("Первая строка\nВторая строка", categories[0].changes[0]["message"])

        for body, error in (
            (":cl: Иван\n- add: Рыба", "должен начинаться со строки :ci: Автор"),
            (":ci:\n- add: Рыба", "необходимо указать имя автора"),
        ):
            with self.subTest(body=body), self.assertRaisesRegex(ValueError, error):
                changelog_actions.parse_manual_changelog(body)

    def test_manual_changelog_is_written_once_per_actions_run(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_dir = repo_root / changelog_actions.CHANGELOG_PATH
            parts_dir = repo_root / changelog_actions.PARTS_PATH
            parts_dir.mkdir(parents=True)
            for filename in ("ChangelogSunrise.yml", "Admin.yml"):
                (changelog_dir / filename).write_text("Entries: []\n", encoding="utf-8")

            categories = {"Main": "ChangelogSunrise.yml", "Admin": "Admin.yml"}
            body = ":ci: Иван\n- add: Рыба\nADMIN:\n- fix: Команда"
            environment = {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "123456",
            }
            with patch.dict("os.environ", environment):
                self.assertEqual(2, changelog_actions.write_manual_parts(repo_root, body, categories))

            main_part = yaml.safe_load((parts_dir / "manual-123456-Main.yml").read_text(encoding="utf-8"))
            admin_part = yaml.safe_load((parts_dir / "manual-123456-Admin.yml").read_text(encoding="utf-8"))
            self.assertEqual("Иван", main_part["author"])
            self.assertNotIn("category", main_part)
            self.assertEqual("Admin", admin_part["category"])
            self.assertEqual(
                "https://github.com/makura-games/sunrise-station/actions/runs/123456",
                main_part["url"],
            )

            (changelog_dir / "ChangelogSunrise.yml").write_text(
                yaml.safe_dump({"Entries": [{"url": main_part["url"]}]}),
                encoding="utf-8",
            )
            (changelog_dir / "Admin.yml").write_text(
                yaml.safe_dump({"Entries": [{"url": admin_part["url"]}]}),
                encoding="utf-8",
            )
            for path in parts_dir.glob("*.yml"):
                path.unlink()

            with patch.dict("os.environ", environment):
                self.assertEqual(0, changelog_actions.write_manual_parts(repo_root, body, categories))
            self.assertEqual([], list(parts_dir.glob("*.yml")))

    def test_unchanged_changelog_template_is_logged_as_skip(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            template_path = repo_root / changelog_actions.PULL_REQUEST_TEMPLATE_PATH
            template_path.parent.mkdir(parents=True)
            template_path.write_text(
                "Описание\n\n:cl: ВАШЕ_ИМЯ\n- add: ТЕКСТ\n- fix: ТЕКСТ\n",
                encoding="utf-8",
            )
            pull_request = {
                "number": 123,
                "merged": True,
                "merged_at": "2026-08-08T12:00:00Z",
                "body": (
                    "Заполненное описание\n\n"
                    ":cl:\u200b ВАШЕ_ИМЯ\n"
                    "\t-\tadd:\u00a0ТЕКСТ\n"
                    " - fix: ТЕКСТ\ufeff\n"
                ),
                "html_url": "https://github.com/makura-games/sunrise-station/pull/123",
                "user": {"login": "Tester"},
                "base": {"ref": "master"},
            }
            output = io.StringIO()

            with redirect_stdout(output):
                written = changelog_actions.write_pull_request_parts(repo_root, [pull_request], "master")

        self.assertEqual(0, written)
        self.assertIn("::notice::PR #123 пропущен: оставлен шаблон чейнжлога.", output.getvalue())

    def test_pull_request_without_marker_is_logged_as_skip(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            (repo_root / changelog_actions.CHANGELOG_PATH).mkdir(parents=True)
            (repo_root / changelog_actions.PARTS_PATH).mkdir(parents=True)
            pull_request = {
                "number": 123,
                "merged": True,
                "merged_at": "2026-08-08T12:00:00Z",
                "body": "Изменение без чейнджлога",
                "html_url": "https://github.com/makura-games/sunrise-station/pull/123",
                "user": {"login": "Tester"},
                "base": {"ref": "master"},
            }
            output = io.StringIO()

            with redirect_stdout(output):
                written = changelog_actions.write_pull_request_parts(repo_root, [pull_request], "master")

        self.assertEqual(0, written)
        self.assertIn("::notice::PR #123 пропущен: отсутствует маркер :cl: или 🆑.", output.getvalue())

    def test_pull_request_with_malformed_entry_is_an_error(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            (repo_root / changelog_actions.CHANGELOG_PATH).mkdir(parents=True)
            (repo_root / changelog_actions.PARTS_PATH).mkdir(parents=True)
            pull_request = {
                "number": 123,
                "merged": True,
                "merged_at": "2026-08-08T12:00:00Z",
                "body": ":cl: Tester\n- add:",
                "html_url": "https://github.com/makura-games/sunrise-station/pull/123",
                "user": {"login": "Tester"},
                "base": {"ref": "master"},
            }

            with self.assertRaisesRegex(RuntimeError, "PR #123: не удалось распознать строку чейнжлога"):
                changelog_actions.write_pull_request_parts(
                    repo_root,
                    [pull_request],
                    "master",
                    current_pull_request_number=123,
                )

    def test_malformed_old_pull_request_does_not_block_current_one(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            (repo_root / changelog_actions.CHANGELOG_PATH).mkdir(parents=True)
            (repo_root / changelog_actions.PARTS_PATH).mkdir(parents=True)
            old_pull_request = {
                "number": 123,
                "merged": True,
                "merged_at": "2026-08-08T12:00:00Z",
                "body": ":cl: Tester\n- add:",
                "html_url": "https://github.com/makura-games/sunrise-station/pull/123",
                "user": {"login": "Tester"},
                "base": {"ref": "master"},
            }
            current_pull_request = {
                "number": 124,
                "merged": True,
                "merged_at": "2026-08-08T13:00:00Z",
                "body": ":cl: Tester\n- add: Новая запись",
                "html_url": "https://github.com/makura-games/sunrise-station/pull/124",
                "user": {"login": "Tester"},
                "base": {"ref": "master"},
            }
            output = io.StringIO()

            with redirect_stdout(output):
                written = changelog_actions.write_pull_request_parts(
                    repo_root,
                    [old_pull_request, current_pull_request],
                    "master",
                    current_pull_request_number=124,
                )

            part = repo_root / changelog_actions.PARTS_PATH / "pr-124-Main.yml"
            self.assertTrue(part.exists())

        self.assertEqual(1, written)
        self.assertIn("::warning::PR #123 пропущен из-за ошибки разбора", output.getvalue())

    def test_status_is_written_to_actions_log_and_summary(self):
        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "summary.md"
            output = io.StringIO()

            with patch.dict("os.environ", {"GITHUB_STEP_SUMMARY": str(summary_path)}), redirect_stdout(output):
                changelog_actions.report_status("success", "Чейнджлог обновлён.")

            self.assertEqual("::notice::Чейнджлог обновлён.\n", output.getvalue())
            self.assertEqual(
                "## Автоматический чейнджлог\n\n- ✅ Чейнджлог обновлён.\n",
                summary_path.read_text(encoding="utf-8"),
            )

    def test_runtime_error_is_reported_to_actions(self):
        output = io.StringIO()

        with patch.object(changelog_actions, "main", side_effect=RuntimeError("не удалось разобрать PR")), redirect_stdout(
            output,
        ):
            with self.assertRaisesRegex(RuntimeError, "не удалось разобрать PR"):
                changelog_actions.run()

        self.assertIn("::error::Чейнджлог завершился с ошибкой: не удалось разобрать PR", output.getvalue())

    def test_active_sunrise_config_supports_emoji_header_and_keeps_extras_in_main(self):
        parsed = changelog_actions.parse_pr_body(
            "🆑\nADMIN:\n- add: Added",
            "Fallback",
        )

        self.assertEqual(
            (
                "Fallback",
                [
                    changelog_actions.ParsedCategory(
                        "Main",
                        [{"type": "Add", "message": "Added"}],
                    ),
                ],
            ),
            parsed,
        )

    def test_parts_are_processed_by_existing_updater_and_are_idempotent(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_dir = repo_root / changelog_actions.CHANGELOG_PATH
            parts_dir = repo_root / changelog_actions.PARTS_PATH
            updater_path = repo_root / changelog_actions.UPDATER_PATH
            parts_dir.mkdir(parents=True)
            updater_path.parent.mkdir(parents=True)

            for filename in changelog_actions.CATEGORY_FILES.values():
                (changelog_dir / filename).write_text("Entries: []\n", encoding="utf-8")
            updater_path.write_bytes(
                (REPO_ROOT / changelog_actions.UPDATER_PATH).read_bytes(),
            )
            updater_path.with_name("changelog_schema.py").write_bytes(
                (REPO_ROOT / "Tools/_sunrise/changelog/changelog_schema.py").read_bytes(),
            )

            pull_request = {
                "number": 123,
                "merged": True,
                "merged_at": "2026-07-29T11:16:50Z",
                "body": (
                    "## Медиа\n"
                    "![Рыба](https://example.org/fish.png)\n"
                    ":cl: Tester\n"
                    "- add: Added"
                ),
                "html_url": "https://github.com/makura-games/sunrise-station/pull/123",
                "user": {"login": "Fallback"},
                "base": {"ref": "master"},
            }

            self.assertEqual(
                1,
                changelog_actions.write_pull_request_parts(repo_root, [pull_request], "master"),
            )
            changelog_actions.update_changelogs(repo_root)

            document = yaml.safe_load(
                (changelog_dir / "ChangelogSunrise.yml").read_text(encoding="utf-8-sig"),
            )
            self.assertEqual("Tester", document["Entries"][0]["author"])
            self.assertEqual("Add", document["Entries"][0]["changes"][0]["type"])
            self.assertEqual(
                [{"url": "https://example.org/fish.png", "description": "Рыба"}],
                document["Entries"][0]["media"],
            )
            self.assertEqual("2026-07-29T11:16:50.0000000+00:00", document["Entries"][0]["time"])
            self.assertEqual(
                0,
                changelog_actions.write_pull_request_parts(repo_root, [pull_request], "master"),
            )

    def test_updater_reports_multiple_categories_without_corrupting_json(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_dir = repo_root / changelog_actions.CHANGELOG_PATH
            parts_dir = repo_root / changelog_actions.PARTS_PATH
            updater_path = repo_root / changelog_actions.UPDATER_PATH
            parts_dir.mkdir(parents=True)
            updater_path.parent.mkdir(parents=True)
            updater_path.write_bytes((REPO_ROOT / changelog_actions.UPDATER_PATH).read_bytes())
            updater_path.with_name("changelog_schema.py").write_bytes(
                (REPO_ROOT / "Tools/_sunrise/changelog/changelog_schema.py").read_bytes(),
            )
            (changelog_dir / "ChangelogSunrise.yml").write_text("Entries: []\n", encoding="utf-8")
            (changelog_dir / "Fish.yml").write_text("Entries: []\n", encoding="utf-8")
            (parts_dir / "fish.yml").write_text(
                "category: Fish\nauthor: Рыба\nchanges:\n- type: Add\n  message: Добавлено\n",
                encoding="utf-8",
            )

            reports = changelog_actions.update_changelogs(
                repo_root,
                {changelog_actions.MAIN_CATEGORY: "ChangelogSunrise.yml", "Fish": "Fish.yml"},
            )

        self.assertEqual([0, 1], [report["added"] for report in reports])

    def test_updater_handles_null_entries_deterministically_and_keeps_unicode(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            changelog = root / "Changelog.yml"
            parts = root / "Parts"
            parts.mkdir()
            changelog.write_text("Entries:\nOther: Значение\n", encoding="utf-8")
            long_message = "Длинное описание без искусственного разрыва. " * 8
            for filename, author in (("z.yml", "Яна"), ("a.yml", "Алиса")):
                (parts / filename).write_text(
                    yaml.safe_dump(
                        {"author": author, "changes": [{"type": "Add", "message": long_message}]},
                        allow_unicode=True,
                        sort_keys=False,
                    ),
                    encoding="utf-8",
                )

            subprocess.run(
                [
                    sys.executable,
                    str(REPO_ROOT / "Tools/_sunrise/changelog/update_changelog.py"),
                    str(changelog),
                    str(parts),
                ],
                check=True,
                capture_output=True,
                text=True,
            )

            text = changelog.read_text(encoding="utf-8-sig")
            document = yaml.safe_load(text)

        self.assertEqual(["Алиса", "Яна"], [entry["author"] for entry in document["Entries"]])
        self.assertEqual([1, 2], [entry["id"] for entry in document["Entries"]])
        self.assertLess(text.index("Entries:"), text.index("Other:"))
        self.assertIn("Алиса", text)
        message_lines = [line for line in text.splitlines() if line.lstrip().startswith("message:")]
        self.assertEqual(2, len(message_lines))
        self.assertTrue(all(long_message in line for line in message_lines))

    def test_updater_repairs_missing_nested_and_duplicate_ids(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            changelog = root / "Changelog.yml"
            parts = root / "Parts"
            parts.mkdir()
            changelog.write_text(
                """Entries:
- author: Без идентификатора
  time: '2026-08-16T10:30:08+00:00'
  changes:
  - type: Add
    message: Добавлено
    id: 7
- author: С идентификатором
  time: '2026-08-16T11:30:08+00:00'
  changes:
  - type: Fix
    message: Исправлено
  id: 7
- author: С повтором
  time: '2026-08-16T12:30:08+00:00'
  changes:
  - type: Tweak
    message: Изменено
  id: 7
""",
                encoding="utf-8",
            )
            (parts / "part.yml").write_text(
                "author: Новая запись\nchanges:\n- type: Add\n  message: Добавлено\n",
                encoding="utf-8",
            )

            result = subprocess.run(
                [
                    sys.executable,
                    str(REPO_ROOT / "Tools/_sunrise/changelog/update_changelog.py"),
                    str(changelog),
                    str(parts),
                ],
                check=True,
                capture_output=True,
                text=True,
            )
            document = yaml.safe_load(changelog.read_text(encoding="utf-8-sig"))
            report = json.loads(result.stdout)

        self.assertEqual([7, 8, 9, 10], [entry["id"] for entry in document["Entries"]])
        self.assertNotIn("id", document["Entries"][1]["changes"][0])
        self.assertEqual(
            ["nested_id", "missing_id", "duplicate_id"],
            [repair["code"] for repair in report["repairs"]],
        )

    def test_updater_reports_non_mapping_changelog_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            changelog = root / "Changelog.yml"
            parts = root / "Parts"
            parts.mkdir()
            changelog.write_text("- broken\n", encoding="utf-8")

            result = subprocess.run(
                [
                    sys.executable,
                    str(REPO_ROOT / "Tools/_sunrise/changelog/update_changelog.py"),
                    str(changelog),
                    str(parts),
                ],
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                env={**os.environ, "PYTHONIOENCODING": "utf-8"},
            )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("Корень чейнжлога должен быть YAML-отображением", result.stderr)

    def test_changelog_targets_are_sorted_and_require_complete_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            targets = repo_root / changelog_targets.TARGETS_PATH
            changelog = repo_root / "Resources/Changelog/ChangelogSunrise.yml"
            targets.mkdir(parents=True)
            changelog.parent.mkdir(parents=True)
            changelog.write_text("Entries: []\n", encoding="utf-8")
            for target_id in ("sunrise", "fish"):
                (targets / f"{target_id}.yml").write_text(
                    "changelog_file: Resources/Changelog/ChangelogSunrise.yml\n"
                    f"webhook_secret: CHANGELOG_DISCORD_WEBHOOK_{target_id.upper()}\n",
                    encoding="utf-8",
                )
            (targets / "Fish_Test.v2.yml").write_text(
                "changelog_file: Resources/Changelog/ChangelogSunrise.yml\n"
                "webhook_secret: ANY_SECRET_NAME\n",
                encoding="utf-8",
            )
            (targets / "example.yml").write_text("# Только комментарии\n", encoding="utf-8")
            (targets / "empty.yml").write_text("", encoding="utf-8")

            self.assertEqual(
                ["Fish_Test.v2.yml", "fish.yml", "sunrise.yml"],
                [path.name for path in changelog_targets.target_paths(repo_root)],
            )
            self.assertEqual(
                "ANY_SECRET_NAME",
                changelog_targets.resolve_target(repo_root, "Fish_Test.v2").webhook_secret,
            )
            self.assertEqual(
                "CHANGELOG_DISCORD_WEBHOOK_SUNRISE",
                changelog_targets.resolve_target(repo_root, "sunrise").webhook_secret,
            )

            (targets / "broken.yml").write_text(
                "changelog_file: Resources/Changelog/ChangelogSunrise.yml\n",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(RuntimeError, "webhook_secret"):
                changelog_targets.resolve_target(repo_root, "broken")

    def test_dispatcher_attempts_remaining_targets_after_failure(self):
        targets = [
            changelog_targets.ChangelogTarget(
                "fish",
                Path("Resources/Changelog/ChangelogFish.yml"),
                "CHANGELOG_DISCORD_WEBHOOK_FISH",
            ),
            changelog_targets.ChangelogTarget(
                "sunrise",
                Path("Resources/Changelog/ChangelogSunrise.yml"),
                "CHANGELOG_DISCORD_WEBHOOK_SUNRISE",
            ),
        ]
        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/fish-station",
                "GITHUB_TOKEN": "token",
                "SOURCE_WORKFLOW_RUN_ID": "123",
                "TARGET_WORKFLOW_REF": "master",
            },
        ), patch.object(
            dispatch_changelogs,
            "resolve_released_sha",
            return_value="a" * 40,
        ), patch.object(
            dispatch_changelogs,
            "target_paths",
            return_value=[Path("fish.yml"), Path("sunrise.yml")],
        ), patch.object(
            dispatch_changelogs,
            "load_target",
            side_effect=targets,
        ), patch.object(
            dispatch_changelogs,
            "dispatch_target",
            side_effect=[RuntimeError("ошибка Fish"), None],
        ) as dispatch:
            with self.assertRaisesRegex(RuntimeError, "fish.yml"):
                dispatch_changelogs.main()

        self.assertEqual(2, dispatch.call_count)

    def test_github_outputs_reject_changelog_path_with_line_break(self):
        target = changelog_targets.ChangelogTarget(
            "target",
            Path("Resources/Changelog/Bad\nPath.yml"),
            "WEBHOOK_SECRET",
        )
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            with patch.dict("os.environ", {"GITHUB_OUTPUT": str(output)}):
                with self.assertRaisesRegex(RuntimeError, "перенос строки"):
                    changelog_targets.write_github_outputs(target)

            self.assertFalse(output.exists())

    def test_reconciliation_includes_equal_timestamps_and_stops_on_older_updates(self):
        checkpoint = datetime(2026, 7, 29, 11, 16, 50, tzinfo=timezone.utc)
        page = [
            {
                "number": 2,
                "updated_at": "2026-07-30T00:00:00Z",
                "merged_at": "2026-07-29T11:16:50Z",
            },
            {
                "number": 1,
                "updated_at": "2026-07-28T00:00:00Z",
                "merged_at": "2026-07-28T00:00:00Z",
            },
        ]

        with patch.dict("os.environ", {"GITHUB_REPOSITORY": "makura-games/sunrise-station"}), patch.object(
            changelog_actions,
            "github_request",
            return_value=page,
        ) as request:
            result = changelog_actions.list_merged_pull_requests(checkpoint)

        self.assertEqual([2], [item["number"] for item in result])
        request.assert_called_once()

    def test_checkpoint_uses_previous_successful_actions_run(self):
        response = {
            "workflow_runs": [
                {
                    "id": 200,
                    "event": "pull_request_target",
                    "created_at": "2026-08-11T13:00:00Z",
                    "run_started_at": "2026-08-11T13:01:00Z",
                },
                {
                    "id": 150,
                    "event": "workflow_dispatch",
                    "created_at": "2026-08-11T12:30:00Z",
                    "run_started_at": "2026-08-11T12:31:00Z",
                },
                {
                    "id": 125,
                    "event": "pull_request_target",
                    "created_at": "2026-08-11T12:15:00Z",
                    "run_started_at": "2026-08-11T12:16:00Z",
                },
                {
                    "id": 100,
                    "event": "pull_request_target",
                    "created_at": "2026-08-11T12:00:00Z",
                    "run_started_at": "2026-08-11T12:01:00Z",
                },
            ],
        }

        def request_response(path, *_args, **_kwargs):
            if path.endswith("/actions/workflows/sunrise-changelog.yml/runs"):
                return response
            if path.endswith("/actions/runs/125/jobs"):
                return {
                    "jobs": [
                        {"name": "update", "status": "completed", "conclusion": "skipped"},
                    ],
                }
            if path.endswith("/actions/runs/100/jobs"):
                return {
                    "jobs": [
                        {"name": "update", "status": "completed", "conclusion": "success"},
                    ],
                }
            self.fail(f"Неожиданный запрос: {path}")

        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "200",
            },
        ), patch.object(changelog_actions, "github_request", side_effect=request_response) as request:
            checkpoint = changelog_actions.load_checkpoint(Path("."))

        self.assertEqual(datetime(2026, 8, 11, 12, 1, tzinfo=timezone.utc), checkpoint)
        self.assertEqual(
            [
                call(
                    "/repos/makura-games/sunrise-station/actions/workflows/sunrise-changelog.yml/runs",
                    {"status": "success", "per_page": 100},
                    token_environment="ACTIONS_TOKEN",
                ),
                call(
                    "/repos/makura-games/sunrise-station/actions/runs/125/jobs",
                    {"per_page": 100},
                    token_environment="ACTIONS_TOKEN",
                ),
                call(
                    "/repos/makura-games/sunrise-station/actions/runs/100/jobs",
                    {"per_page": 100},
                    token_environment="ACTIONS_TOKEN",
                ),
            ],
            request.call_args_list,
        )

    def test_discord_api_rejects_unsafe_changelog_path(self):
        session = Mock()

        with patch.object(discord_changelog, "CHANGELOG_FILE", "../outside.yml"):
            with self.assertRaisesRegex(RuntimeError, "относительным путём внутри репозитория"):
                discord_changelog.get_last_changelog_by_sha(session, "abc", "makura-games/repo")

        session.get.assert_not_called()

    def test_discord_api_encodes_changelog_path(self):
        session = Mock()

        discord_changelog.get_last_changelog_by_sha(
            session,
            "abc",
            "makura-games/repo",
            Path("Resources/Changelog/Changelog?test.yml"),
        )

        session.get.assert_called_once_with(
            "https://api.github.com/repos/makura-games/repo/contents/"
            "Resources/Changelog/Changelog%3Ftest.yml",
            headers={"Accept": "application/vnd.github.raw"},
            params={"ref": "abc"},
            timeout=discord_changelog.HTTP_REQUEST_TIMEOUT,
        )

    def test_discord_history_paginates_until_matching_target(self):
        current = {"id": 300}
        fish_sha = "f" * 40
        sunrise_sha = "a" * 40
        first_page = {
            "workflow_runs": [
                {
                    "id": run_id,
                    "display_title": f"Discord changelog sunrise for {sunrise_sha}",
                }
                for run_id in range(100, 200)
            ],
        }
        second_page = {
            "workflow_runs": [
                {
                    "id": 200,
                    "display_title": f"Discord changelog Fish_Test.v2 for {fish_sha}",
                },
            ],
        }
        session = Mock()
        with patch.object(
            discord_changelog,
            "get_current_run",
            return_value=current,
        ), patch.object(
            discord_changelog,
            "get_past_runs",
            side_effect=[first_page, second_page],
        ) as past_runs:
            result = discord_changelog.get_most_recent_workflow(
                session,
                "makura-games/fish-station",
                "300",
                "Fish_Test.v2",
            )

        self.assertEqual(200, result["id"])
        self.assertEqual(
            [call(session, current, 1), call(session, current, 2)],
            past_runs.call_args_list,
        )

    def test_discord_history_requests_one_hundred_runs_per_page(self):
        session = Mock()
        session.get.return_value.json.return_value = {"workflow_runs": []}
        current = {
            "created_at": "2026-08-15T00:00:00Z",
            "workflow_url": "https://api.github.test/workflows/1",
        }

        discord_changelog.get_past_runs(session, current, 2)

        session.get.assert_called_once_with(
            "https://api.github.test/workflows/1/runs",
            params={
                "status": "success",
                "created": "<=2026-08-15T00:00:00Z",
                "per_page": 100,
                "page": 2,
            },
            timeout=discord_changelog.HTTP_REQUEST_TIMEOUT,
        )

    def test_discord_runtime_requires_webhook(self):
        with patch.dict(
            "os.environ",
            {
                "CHANGELOG_FILE": "Resources/Changelog/ChangelogSunrise.yml",
                "CHANGELOG_TARGET_ID": "sunrise",
                "RELEASED_SHA": "a" * 40,
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "300",
                "GITHUB_TOKEN": "token",
                "SOURCE_WORKFLOW_RUN_ID": "200",
                "DISCORD_WEBHOOK_URL": "",
            },
            clear=True,
        ):
            with self.assertRaisesRegex(RuntimeError, "DISCORD_WEBHOOK_URL"):
                discord_changelog.validate_runtime_environment()

    def test_first_discord_workflow_uses_previous_stable_publish(self):
        previous = {"id": 100, "head_commit": {"id": "abc"}}
        first_attempt = {
            "id": 250,
            "display_title": f"Discord changelog sunrise for {'b' * 40}",
        }

        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "300",
                "GITHUB_TOKEN": "token",
                "SOURCE_WORKFLOW_RUN_ID": "200",
                "CHANGELOG_TARGET_ID": "sunrise",
            },
        ), patch.object(
            discord_changelog,
            "get_most_recent_workflow",
            return_value=None,
        ) as recent, patch.object(
            discord_changelog,
            "get_earliest_target_attempt",
            return_value=first_attempt,
        ) as earliest, patch.object(
            discord_changelog,
            "get_source_release_before_attempt",
            return_value=previous,
        ) as source, patch.object(
            discord_changelog,
            "get_last_changelog_by_sha",
            return_value="Entries: []",
        ) as load:
            result = discord_changelog.get_last_changelog(Path("Resources/Changelog/ChangelogSunrise.yml"))

        self.assertEqual("Entries: []", result)
        recent.assert_called_once()
        self.assertEqual("300", recent.call_args.args[2])
        self.assertEqual("sunrise", recent.call_args.args[3])
        self.assertEqual(("300", "sunrise"), earliest.call_args.args[2:])
        self.assertEqual(("200", first_attempt), source.call_args.args[2:])
        self.assertEqual("abc", load.call_args.args[1])
        self.assertTrue(load.call_args.kwargs["allow_missing"])

    def test_discord_workflow_keeps_last_successful_released_sha(self):
        sha = "a" * 40
        previous = {
            "id": 250,
            "display_title": f"Discord changelog sunrise for {sha}",
            "head_commit": {"id": "default-branch-sha"},
        }

        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "300",
                "GITHUB_TOKEN": "token",
                "SOURCE_WORKFLOW_RUN_ID": "200",
                "CHANGELOG_TARGET_ID": "sunrise",
            },
        ), patch.object(
            discord_changelog,
            "get_most_recent_workflow",
            return_value=previous,
        ) as recent, patch.object(
            discord_changelog,
            "get_last_changelog_by_sha",
            return_value="Entries: []",
        ) as load:
            discord_changelog.get_last_changelog(Path("Resources/Changelog/ChangelogSunrise.yml"))

        recent.assert_called_once()
        self.assertEqual("sunrise", recent.call_args.args[3])
        self.assertEqual(sha, load.call_args.args[1])
        self.assertFalse(load.call_args.kwargs["allow_missing"])

    def test_discord_workflow_falls_back_when_previous_title_has_no_sha(self):
        previous_discord = {"id": 250, "display_title": "Старое имя запуска"}
        previous_stable = {"id": 100, "head_commit": {"id": "stable-sha"}}
        first_attempt = {
            "id": 251,
            "display_title": f"Discord changelog sunrise for {'b' * 40}",
        }

        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "GITHUB_RUN_ID": "300",
                "GITHUB_TOKEN": "token",
                "SOURCE_WORKFLOW_RUN_ID": "200",
                "CHANGELOG_TARGET_ID": "sunrise",
            },
        ), patch.object(
            discord_changelog,
            "get_most_recent_workflow",
            return_value=previous_discord,
        ) as recent, patch.object(
            discord_changelog,
            "get_earliest_target_attempt",
            return_value=first_attempt,
        ), patch.object(
            discord_changelog,
            "get_source_release_before_attempt",
            return_value=previous_stable,
        ), patch.object(
            discord_changelog,
            "get_last_changelog_by_sha",
            return_value="Entries: []",
        ) as load:
            discord_changelog.get_last_changelog(Path("Resources/Changelog/ChangelogSunrise.yml"))

        recent.assert_called_once()
        self.assertEqual("300", recent.call_args.args[2])
        self.assertEqual("sunrise", recent.call_args.args[3])
        self.assertEqual("stable-sha", load.call_args.args[1])

    def test_checkpoint_falls_back_to_latest_changelog_entry(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_dir = repo_root / changelog_actions.CHANGELOG_PATH
            changelog_dir.mkdir(parents=True)
            (changelog_dir / "ChangelogSunrise.yml").write_text(
                'Entries:\n- time: "2026-08-10T12:00:00Z"\n',
                encoding="utf-8",
            )

            with patch.dict(
                "os.environ",
                {"GITHUB_REPOSITORY": "makura-games/sunrise-station"},
            ), patch.object(
                changelog_actions,
                "github_request",
                return_value={"workflow_runs": []},
            ):
                checkpoint = changelog_actions.load_checkpoint(repo_root)

        self.assertEqual(datetime(2026, 8, 10, 12, tzinfo=timezone.utc), checkpoint)

    def test_main_resolves_repository_root_after_tool_move(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_dir = repo_root / changelog_actions.CHANGELOG_PATH
            parts_dir = repo_root / changelog_actions.PARTS_PATH
            updater_path = repo_root / changelog_actions.UPDATER_PATH
            changelog_dir.mkdir(parents=True)
            parts_dir.mkdir(parents=True)
            updater_path.parent.mkdir(parents=True)
            updater_path.write_bytes(
                (REPO_ROOT / changelog_actions.UPDATER_PATH).read_bytes(),
            )
            updater_path.with_name("changelog_schema.py").write_bytes(
                (REPO_ROOT / "Tools/_sunrise/changelog/changelog_schema.py").read_bytes(),
            )
            (changelog_dir / "ChangelogSunrise.yml").write_text("Entries: []\n", encoding="utf-8")
            event_path = repo_root / "event.json"
            event_path.write_text("{}", encoding="utf-8")
            script_path = repo_root / "Tools/_sunrise/changelog/changelog_actions.py"

            with patch.object(changelog_actions, "__file__", str(script_path)), patch.object(
                sys,
                "argv",
                ["changelog_actions.py", "--event-path", str(event_path)],
            ), patch.object(
                changelog_actions,
                "load_checkpoint",
                return_value=datetime(2026, 8, 11, tzinfo=timezone.utc),
            ), patch.object(changelog_actions, "list_merged_pull_requests", return_value=[]):
                changelog_actions.main()

            self.assertEqual(
                {"Entries": []},
                yaml.safe_load((changelog_dir / "ChangelogSunrise.yml").read_text(encoding="utf-8")),
            )

    def test_discord_rate_limit_retries_are_bounded(self):
        response = Mock(status_code=429)
        response.json.return_value = {"retry_after": 0}
        response.raise_for_status.side_effect = discord_changelog.requests.HTTPError("rate limited")

        with patch.object(discord_changelog.requests, "post", return_value=response) as post, patch.object(
            discord_changelog.time,
            "sleep",
        ):
            with self.assertRaises(discord_changelog.requests.HTTPError):
                discord_changelog.send_embed_discord({"description": "test"})

        self.assertEqual(discord_changelog.DISCORD_RETRY_LIMIT + 1, post.call_count)
        self.assertTrue(
            all(call.kwargs["timeout"] == discord_changelog.HTTP_REQUEST_TIMEOUT for call in post.call_args_list),
        )

    def test_large_discord_retry_after_fails_without_sleeping_past_deadline(self):
        response = Mock(status_code=429)
        response.json.return_value = {"retry_after": 10**1_000}
        response.raise_for_status.side_effect = discord_changelog.requests.HTTPError("rate limited")

        with patch.object(discord_changelog.requests, "post", return_value=response), patch.object(
            discord_changelog.time,
            "monotonic",
            return_value=100,
        ), patch.object(discord_changelog.time, "sleep") as sleep:
            with self.assertRaises(discord_changelog.requests.HTTPError):
                discord_changelog.send_embed_discord({"description": "test"}, deadline=110)

        sleep.assert_not_called()

    def test_invalid_discord_retry_after_uses_safe_default(self):
        invalid_values = ("invalid", float("nan"), -1, True, None)

        for retry_after in invalid_values:
            with self.subTest(retry_after=retry_after):
                rate_limited = Mock(status_code=429)
                rate_limited.json.return_value = {"retry_after": retry_after}
                sent = Mock(status_code=204)

                with patch.object(
                    discord_changelog.requests,
                    "post",
                    side_effect=(rate_limited, sent),
                ), patch.object(discord_changelog.time, "sleep") as sleep:
                    discord_changelog.send_embed_discord({"description": "test"})

                sleep.assert_called_once_with(1)

    def test_discord_accepts_message_and_no_content_statuses(self):
        for status_code in (200, 204):
            with self.subTest(status_code=status_code):
                response = Mock(status_code=status_code)
                with patch.object(discord_changelog.requests, "post", return_value=response):
                    discord_changelog.send_embed_discord({"description": "test"})

    def test_unexpected_discord_status_keeps_status_code(self):
        response = Mock(status_code=201)

        with patch.object(discord_changelog.requests, "post", return_value=response):
            with self.assertRaises(discord_changelog.UnexpectedDiscordStatusError) as raised:
                discord_changelog.send_embed_discord({"description": "test"})

        self.assertEqual(201, raised.exception.status_code)
        self.assertEqual("Discord webhook вернул неожиданный статус 201", str(raised.exception))

    def test_media_url_rejections_do_not_resolve_or_connect(self):
        invalid_urls = (
            "http://example.org/file.png",
            "https://user:password@example.org/file.png",
            "https://example.org:8443/file.png",
            "https://127.0.0.1/file.png",
        )

        with patch.object(discord_changelog.socket, "getaddrinfo") as resolve, patch.object(
            discord_changelog, "_VerifiedHTTPSConnection"
        ) as connection:
            for url in invalid_urls:
                with self.subTest(url=url), self.assertRaises(discord_changelog.MediaError):
                    discord_changelog.download_media(url)

        resolve.assert_not_called()
        connection.assert_not_called()

    def test_media_dns_rebinding_on_redirect_is_rejected(self):
        redirect = Mock(status=302)
        redirect.getheader.return_value = "https://next.example/file.png"
        connection = Mock()
        connection.getresponse.return_value = redirect

        with patch.object(
            discord_changelog,
            "_resolve_public_address",
            side_effect=[(discord_changelog.socket.AF_INET, "93.184.216.34"), discord_changelog.MediaError("private")],
        ), patch.object(discord_changelog, "_VerifiedHTTPSConnection", return_value=connection) as connect:
            with self.assertRaises(discord_changelog.MediaError):
                discord_changelog.download_media("https://example.org/file.png")

        connect.assert_called_once()
        connection.request.assert_called_once()

    def test_media_redirect_limit_is_checked_before_extra_request(self):
        responses = []
        for index in range(discord_changelog.MEDIA_MAX_REDIRECTS + 1):
            response = Mock(status=302)
            response.getheader.return_value = f"https://redirect-{index}.example/file.png"
            responses.append(response)
        connection = Mock()
        connection.getresponse.side_effect = responses

        with patch.object(
            discord_changelog,
            "_resolve_public_address",
            return_value=(discord_changelog.socket.AF_INET, "93.184.216.34"),
        ), patch.object(discord_changelog, "_VerifiedHTTPSConnection", return_value=connection):
            with self.assertRaisesRegex(discord_changelog.MediaError, "превышено число перенаправлений"):
                discord_changelog.download_media("https://example.org/file.png")

        self.assertEqual(discord_changelog.MEDIA_MAX_REDIRECTS + 1, connection.request.call_count)

    def test_media_size_and_signature_are_checked_after_streaming(self):
        too_large = Mock(status=200)
        too_large.getheader.side_effect = lambda name: str(discord_changelog.MEDIA_MAX_SIZE + 1) if name == "Content-Length" else None
        too_large_connection = Mock()
        too_large_connection.getresponse.return_value = too_large

        with patch.object(
            discord_changelog,
            "_resolve_public_address",
            return_value=(discord_changelog.socket.AF_INET, "93.184.216.34"),
        ), patch.object(discord_changelog, "_VerifiedHTTPSConnection", return_value=too_large_connection):
            with self.assertRaisesRegex(discord_changelog.MediaError, "превышает лимит"):
                discord_changelog.download_media("https://example.org/file.bin")
        too_large.read.assert_not_called()

        unknown = Mock(status=200)
        unknown.getheader.return_value = None
        unknown.read.side_effect = [b"not a supported file", b""]
        unknown_connection = Mock()
        unknown_connection.getresponse.return_value = unknown
        with patch.object(
            discord_changelog,
            "_resolve_public_address",
            return_value=(discord_changelog.socket.AF_INET, "93.184.216.34"),
        ), patch.object(discord_changelog, "_VerifiedHTTPSConnection", return_value=unknown_connection):
            with self.assertRaisesRegex(discord_changelog.MediaError, "сигнатура файла"):
                discord_changelog.download_media("https://example.org/file.bin")

    def test_media_payload_uses_attachments_for_images_and_videos(self):
        image = discord_changelog.DownloadedMedia(
            "https://example.org/image.png", "Изображение", b"png", "image/png", "media-1.png"
        )
        video = discord_changelog.DownloadedMedia(
            "https://example.org/video.mp4", None, b"mp4", "video/mp4", "media-2.mp4"
        )
        text_embed = {"title": "Автор", "description": "Текст"}

        payload, files = discord_changelog.build_media_payload(text_embed, [image, video])

        container = payload["components"][0]
        self.assertEqual(discord_changelog.DISCORD_COMPONENTS_V2_FLAG, payload["flags"])
        self.assertEqual("**Автор**", container["components"][0]["content"])
        self.assertEqual({"type": 14, "divider": True, "spacing": 1}, container["components"][1])
        self.assertEqual("Текст", container["components"][2]["content"])
        self.assertEqual(
            "attachment://media-1.png",
            container["components"][3]["items"][0]["media"]["url"],
        )
        self.assertEqual(
            "attachment://media-2.mp4",
            container["components"][3]["items"][1]["media"]["url"],
        )
        self.assertEqual(
            [
                {"id": 0, "filename": "media-1.png", "description": "Изображение"},
                {"id": 1, "filename": "media-2.mp4"},
            ],
            payload["attachments"],
        )
        self.assertEqual(["files[0]", "files[1]"], [field for field, _ in files])
        self.assertEqual(("media-2.mp4", b"mp4", "video/mp4"), files[1][1])

    def test_pr_media_gallery_is_rendered_last_after_divider(self):
        image = discord_changelog.DownloadedMedia(
            "image", "Изображение", b"png", "image/png", "media-1.png"
        )
        entry = {
            "author": "Tester",
            "changes": [{"type": "Add", "message": "Добавлена рыба"}],
            "url": "https://example.org/pr/1",
        }

        payload, _ = discord_changelog.build_media_payload(
            {"title": "👤 Tester", "description": ""},
            [image],
            entry,
        )

        components = payload["components"][0]["components"]
        self.assertEqual({"type": 14, "divider": True, "spacing": 1}, components[1])
        self.assertEqual("💡 **Добавлено**\n• Добавлена рыба", components[2]["content"])
        self.assertEqual("[GitHub Pull Request](https://example.org/pr/1)", components[3]["content"])
        self.assertEqual({"type": 14, "divider": True, "spacing": 2}, components[4])
        self.assertEqual("attachment://media-1.png", components[5]["items"][0]["media"]["url"])

    def test_media_gallery_keeps_author_order_regardless_of_type(self):
        video = discord_changelog.DownloadedMedia(
            "video", None, b"webm", "video/webm", "media-1.webm"
        )
        image = discord_changelog.DownloadedMedia(
            "image", None, b"png", "image/png", "media-2.png"
        )

        payload, _ = discord_changelog.build_media_payload(None, [video, image])

        items = payload["components"][0]["components"][0]["items"]
        self.assertEqual(
            ["attachment://media-1.webm", "attachment://media-2.png"],
            [item["media"]["url"] for item in items],
        )

    def test_remote_service_link_stays_in_authored_order(self):
        youtube = discord_changelog.RemoteMedia(
            "https://www.youtube.com/watch?v=example", "YouTube", change_index=0
        )
        image = discord_changelog.DownloadedMedia(
            "image", None, b"png", "image/png", "media-2.png", change_index=0
        )
        vimeo = discord_changelog.RemoteMedia(
            "https://vimeo.com/example", "Vimeo", change_index=0
        )

        payload, files = discord_changelog.build_media_payload(None, [youtube, image, vimeo])

        components = payload["components"][0]["components"]
        self.assertEqual(
            "🔗 **YouTube**\n<https://www.youtube.com/watch?v=example>",
            components[0]["content"],
        )
        self.assertEqual("attachment://media-2.png", components[1]["items"][0]["media"]["url"])
        self.assertEqual(
            "🔗 **Vimeo**\n<https://vimeo.com/example>",
            components[2]["content"],
        )
        self.assertEqual([{"id": 0, "filename": "media-2.png"}], payload["attachments"])
        self.assertEqual(["files[0]"], [field for field, _ in files])

    def test_unsupported_download_becomes_remote_service_link(self):
        entry = {
            "changes": [{"type": "Add", "message": "Видео"}],
            "media": [{"url": "https://www.youtube.com/watch?v=example", "change": 0}],
        }

        with patch.object(
            discord_changelog,
            "download_media",
            side_effect=discord_changelog.MediaError("сигнатура файла не поддерживается"),
        ):
            batch = next(iter(discord_changelog.iter_entry_media_batches(entry)))

        self.assertEqual(
            discord_changelog.RemoteMedia(
                "https://www.youtube.com/watch?v=example",
                None,
                change_index=0,
            ),
            batch[0],
        )

    def test_remote_only_batch_uses_json_request(self):
        remote = discord_changelog.RemoteMedia("https://www.youtube.com/watch?v=example", None)

        with patch.object(discord_changelog, "_send_discord_payload") as send_json, patch.object(
            discord_changelog,
            "send_multipart_discord",
        ) as send_multipart:
            discord_changelog.send_media_batch([remote], None, discord_changelog.time.monotonic() + 10)

        send_json.assert_called_once()
        send_multipart.assert_not_called()

    def test_media_is_rendered_after_its_changelog_line(self):
        image = discord_changelog.DownloadedMedia(
            "image", None, b"png", "image/png", "media-1.png", change_index=0
        )
        video = discord_changelog.DownloadedMedia(
            "video", None, b"webm", "video/webm", "media-2.webm", change_index=0
        )
        second_image = discord_changelog.DownloadedMedia(
            "second", None, b"png", "image/png", "media-3.png", change_index=2
        )
        entry = {
            "author": "Tester",
            "changes": [
                {"type": "Add", "message": "Первая строка"},
                {"type": "Fix", "message": "Вторая строка"},
                {"type": "Tweak", "message": "Третья строка"},
            ],
            "url": "https://example.org/pr/1",
        }

        payload, _ = discord_changelog.build_media_payload(
            {"title": "👤 Tester", "description": ""},
            [image, video, second_image],
            entry,
        )

        components = payload["components"][0]["components"]
        self.assertEqual("**👤 Tester**", components[0]["content"])
        self.assertEqual({"type": 14, "divider": True, "spacing": 1}, components[1])
        self.assertEqual("💡 **Добавлено**\n• Первая строка", components[2]["content"])
        self.assertEqual("attachment://media-1.png", components[3]["items"][0]["media"]["url"])
        self.assertEqual("attachment://media-2.webm", components[3]["items"][1]["media"]["url"])
        self.assertEqual({"type": 14, "divider": True, "spacing": 2}, components[4])
        self.assertEqual("⚒️ **Изменено**\n• Третья строка", components[5]["content"])
        self.assertEqual("attachment://media-3.png", components[6]["items"][0]["media"]["url"])
        self.assertEqual({"type": 14, "divider": True, "spacing": 2}, components[7])
        self.assertEqual("🪛 **Исправлено**\n• Вторая строка", components[8]["content"])
        self.assertEqual("[GitHub Pull Request](https://example.org/pr/1)", components[9]["content"])

    def test_media_batches_obey_file_count_and_request_size(self):
        def batches_for(media):
            entry = {"media": [{"url": item.url} for item in media]}
            with patch.object(discord_changelog, "download_media", side_effect=media):
                return list(discord_changelog.iter_entry_media_batches(entry))

        small = [
            discord_changelog.DownloadedMedia(str(index), None, b"x", "video/mp4", f"media-{index}.mp4")
            for index in range(11)
        ]
        batches = batches_for(small)
        self.assertEqual([10, 1], [len(batch) for batch in batches])

        item_size = discord_changelog.MEDIA_MAX_REQUEST_SIZE // 8 + 1
        large = [
            discord_changelog.DownloadedMedia(str(index), None, b"x" * item_size, "video/mp4", f"media-{index}.mp4")
            for index in range(9)
        ]
        self.assertEqual([7, 2], [len(batch) for batch in batches_for(large)])

    def test_downloaded_media_keeps_its_change_index(self):
        entry = {
            "changes": [{}, {}],
            "media": [
                {"url": "https://example.org/first.png", "change": 0},
                {"url": "https://example.org/second.webm", "change": 1},
            ],
        }
        media = [
            discord_changelog.DownloadedMedia("first", None, b"png", "image/png", "first.png"),
            discord_changelog.DownloadedMedia("second", None, b"webm", "video/webm", "second.webm"),
        ]

        with patch.object(discord_changelog, "download_media", side_effect=media):
            batch = next(iter(discord_changelog.iter_entry_media_batches(entry)))

        self.assertEqual([0, 1], [item.change_index for item in batch])

    def test_invalid_change_record_is_skipped_when_collecting_media(self):
        output = io.StringIO()

        with patch.object(discord_changelog, "download_media") as download, redirect_stdout(output):
            batches = list(discord_changelog.iter_entry_media_batches({"changes": ["не объект"]}))

        self.assertEqual([], batches)
        download.assert_not_called()
        self.assertIn("запись changes имеет неверный формат", output.getvalue())

    def test_media_count_and_total_deadline_are_bounded(self):
        records = [
            {"url": f"https://example.org/{index}.png"}
            for index in range(discord_changelog.MEDIA_MAX_FILES_PER_ENTRY + 1)
        ]
        media = discord_changelog.DownloadedMedia("url", None, b"png", "image/png", "media.png")

        with patch.object(discord_changelog, "download_media", return_value=media) as download:
            batches = list(discord_changelog.iter_entry_media_batches({"media": records}))

        self.assertEqual(discord_changelog.MEDIA_MAX_FILES_PER_ENTRY, sum(map(len, batches)))
        self.assertEqual(discord_changelog.MEDIA_MAX_FILES_PER_ENTRY, download.call_count)

        with self.assertRaises(discord_changelog.DiscordPublishTimeoutError):
            discord_changelog.download_media(
                "https://example.org/image.png",
                deadline=discord_changelog.time.monotonic() - 1,
            )

    def test_multipart_payload_and_rate_limit_retry(self):
        payload = {"embeds": [{"description": "Текст"}]}
        files = [("files[0]", ("media.png", b"png", "image/png"))]
        limited = Mock(status_code=429)
        limited.json.return_value = {"retry_after": 0}
        sent = Mock(status_code=204)

        with patch.object(discord_changelog.requests, "post", side_effect=[limited, sent]) as post, patch.object(
            discord_changelog.time, "sleep"
        ):
            discord_changelog.send_multipart_discord(
                payload,
                files,
                deadline=discord_changelog.time.monotonic() + 10,
            )

        self.assertEqual(
            {
                "payload_json": json.dumps(
                    {**payload, "allowed_mentions": {"parse": []}},
                    ensure_ascii=False,
                )
            },
            post.call_args_list[0].kwargs["data"],
        )
        self.assertEqual(files, post.call_args_list[0].kwargs["files"])

    def test_components_v2_payload_enables_webhook_components(self):
        payload = {"flags": discord_changelog.DISCORD_COMPONENTS_V2_FLAG, "components": []}

        with (
            patch.object(
                discord_changelog, "DISCORD_WEBHOOK_URL",
                "https://discord.com/api/webhooks/123/test-token",
            ),
            patch.object(
                discord_changelog.requests, "post",
                return_value=Mock(status_code=204),
            ) as post,
        ):
            discord_changelog.send_multipart_discord(
                payload,
                [],
                deadline=discord_changelog.time.monotonic() + 10,
            )

        self.assertEqual(
            "https://discord.com/api/webhooks/123/test-token"
            "?wait=true&with_components=true",
            post.call_args.args[0],
        )

    def test_media_send_failure_falls_back_to_text_and_warns(self):
        entry = {
            "author": "Tester",
            "changes": [{"type": "Add", "message": "Добавлено"}],
            "media": [{"url": "https://example.org/image.png"}],
        }
        media = discord_changelog.DownloadedMedia(
            entry["media"][0]["url"], None, b"png", "image/png", "media-1.png"
        )
        output = io.StringIO()

        with patch.object(discord_changelog, "DISCORD_WEBHOOK_URL", "https://discord.example/webhook"), patch.object(
            discord_changelog, "download_media", return_value=media
        ), patch.object(
            discord_changelog, "send_multipart_discord", side_effect=RuntimeError("Discord недоступен")
        ) as multipart, patch.object(discord_changelog, "send_changelog_text") as text_send, redirect_stdout(output):
            discord_changelog.send_to_discord([entry])

        multipart.assert_called_once()
        text_send.assert_called_once()
        self.assertIn("::warning::Медиа https://example.org/image.png", output.getvalue())

    def test_images_and_videos_are_sent_with_changelog_in_source_order(self):
        image = discord_changelog.DownloadedMedia(
            "https://example.org/image.png", None, b"png", "image/png", "media-1.png"
        )
        video = discord_changelog.DownloadedMedia(
            "https://example.org/video.webm", None, b"webm", "video/webm", "media-2.webm"
        )
        entry = {
            "author": "Tester",
            "changes": [{"type": "Add", "message": "Добавлено"}],
        }

        with patch.object(discord_changelog, "DISCORD_WEBHOOK_URL", "https://discord.example/webhook"), patch.object(
            discord_changelog,
            "iter_entry_media_batches",
            return_value=iter([[image, video]]),
        ), patch.object(discord_changelog, "send_media_batch") as send, patch.object(
            discord_changelog,
            "send_changelog_text",
        ) as send_text:
            discord_changelog.send_to_discord([entry])

        send.assert_called_once()
        self.assertEqual([image, video], send.call_args.args[0])
        self.assertIsNotNone(send.call_args_list[0].args[1])
        send_text.assert_not_called()

    def test_split_text_with_media_uses_only_the_selected_text_part(self):
        media = discord_changelog.DownloadedMedia(
            "https://example.org/image.png", None, b"png", "image/png", "media-1.png"
        )
        entry = {"author": "Tester", "changes": [{"type": "Add", "message": "Добавлено"}]}

        with patch.object(discord_changelog, "DISCORD_WEBHOOK_URL", "https://discord.example/webhook"), patch.object(
            discord_changelog,
            "split_message",
            return_value=["Первая часть", "Последняя часть"],
        ), patch.object(
            discord_changelog,
            "iter_entry_media_batches",
            return_value=iter([[media], [media]]),
        ), patch.object(discord_changelog, "send_media_batch") as send, patch.object(
            discord_changelog,
            "send_changelog_text",
        ) as send_text:
            discord_changelog.send_to_discord([entry])

        self.assertEqual("💡 **Добавлено**\nПоследняя часть", send.call_args_list[0].args[1]["description"])
        self.assertEqual([None, None], [item.args[3] for item in send.call_args_list])
        send_text.assert_called_once()
        self.assertEqual("💡 **Добавлено**\nПервая часть", send_text.call_args.args[0]["description"])

    def test_all_split_text_parts_are_sent_when_there_is_no_media(self):
        entry = {"author": "Tester", "changes": [{"type": "Add", "message": "Добавлено"}]}

        with patch.object(discord_changelog, "DISCORD_WEBHOOK_URL", "https://discord.example/webhook"), patch.object(
            discord_changelog,
            "split_message",
            return_value=["Первая часть", "Последняя часть"],
        ), patch.object(
            discord_changelog,
            "iter_entry_media_batches",
            return_value=iter([]),
        ), patch.object(discord_changelog, "send_changelog_text") as send:
            discord_changelog.send_to_discord([entry])

        self.assertEqual(
            ["💡 **Добавлено**\nПервая часть", "💡 **Добавлено**\nПоследняя часть"],
            [item.args[0]["description"] for item in send.call_args_list],
        )

    def test_long_discord_group_repeats_its_heading_after_split(self):
        parts = discord_changelog.format_grouped_change_parts(
            [{"type": "Fix", "message": "Очень длинное исправление " * 20}],
            limit=100,
        )

        self.assertGreater(len(parts), 1)
        self.assertTrue(all(part.startswith("🪛 **Исправлено**\n") for part in parts))
        self.assertTrue(all(len(part) <= 100 for part in parts))

    def test_discord_groups_changes_in_fixed_order_and_keeps_author_order(self):
        entry = {
            "author": "Tester",
            "changes": [
                {"type": "Remove", "message": "Удалено"},
                {"type": "Add", "message": "Добавлено первым"},
                {"type": "Fix", "message": "Исправлено"},
                {"type": "Tweak", "message": "Изменено"},
                {"type": "Add", "message": "Добавлено вторым"},
                {"type": "Legacy", "message": "Неизвестный старый тип"},
            ],
        }

        with patch.object(discord_changelog, "DISCORD_WEBHOOK_URL", "https://discord.example/webhook"), patch.object(
            discord_changelog,
            "iter_entry_media_batches",
            return_value=iter([]),
        ), patch.object(discord_changelog, "send_changelog_text") as send:
            discord_changelog.send_to_discord([entry])

        embed = send.call_args.args[0]
        self.assertEqual("👤 Tester", embed["title"])
        self.assertEqual(
            """💡 **Добавлено**
• Добавлено первым
• Добавлено вторым

⚒️ **Изменено**
• Изменено

🪛 **Исправлено**
• Исправлено

❌ **Удалено**
• Удалено

❓ **Прочее**
• Неизвестный старый тип""",
            embed["description"],
        )

    def test_discord_skips_invalid_change_records_without_losing_valid_ones(self):
        entry = {
            "changes": [
                "broken",
                {},
                {"type": "Add"},
                {"type": None, "message": "Без типа"},
                {"type": "Add", "message": "Рабочая запись"},
                {"type": "Legacy", "message": "Неизвестный тип"},
            ],
        }

        payload, _ = discord_changelog.build_media_payload(
            {"title": "👤 Tester", "description": ""},
            [],
            entry,
        )
        content = "\n".join(
            component.get("content", "")
            for component in payload["components"][0]["components"]
        )

        self.assertIn("💡 **Добавлено**\n• Рабочая запись", content)
        self.assertIn("❓ **Прочее**\n• Неизвестный тип", content)
        self.assertNotIn("Без типа", content)

    def test_manual_and_reader_timestamps_support_optional_microseconds(self):
        self.assertRegex(manual_changelog.make_timestamp(), r"\.\d{6}\+00:00$")

        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog_path = repo_root / "Resources/Changelog/ChangelogSunrise.yml"
            changelog_path.parent.mkdir(parents=True)
            changelog_path.write_text(
                """Entries:
- author: Tester
  time: "2026-08-08T12:00:00+00:00"
  changes: []
- author: Tester
  time: "2026-08-08T12:00:00.123456+00:00"
  changes: []
""",
                encoding="utf-8",
            )
            result = subprocess.run(
                [sys.executable, str(REPO_ROOT / "Tools/_sunrise/changelog/read_changelog.py")],
                cwd=repo_root,
                check=True,
                capture_output=True,
                text=True,
            )

        self.assertNotIn("Error formatting time", result.stdout)

    def test_workflow_has_all_entry_points_and_safe_app_write_retry(self):
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        publish_workflow = PUBLISH_WORKFLOW_PATH.read_text(encoding="utf-8")
        discord_workflow = DISCORD_WORKFLOW_PATH.read_text(encoding="utf-8")
        dispatch_workflow = DISPATCH_WORKFLOW_PATH.read_text(encoding="utf-8")
        runner = RUNNER_PATH.read_text(encoding="utf-8")
        document = yaml.load(workflow, Loader=yaml.BaseLoader)
        yaml.load(discord_workflow, Loader=yaml.BaseLoader)
        yaml.load(dispatch_workflow, Loader=yaml.BaseLoader)

        self.assertIn("on", document)
        self.assertIn("jobs", document)
        self.assertIn("pull_request_target:\n    types: [closed]\n    branches: [master]", workflow)
        self.assertIn("workflow_dispatch:", workflow)
        self.assertIn("changelog:", workflow)
        self.assertIn("type: string", workflow)
        self.assertIn(":ci: Автор", workflow)
        self.assertIn("Много строк — через gh/API", workflow)
        self.assertIn("if: github.event_name == 'workflow_dispatch' || github.event.pull_request.merged == true", workflow)
        self.assertNotIn("push:", workflow)
        self.assertNotIn("schedule:", workflow)
        self.assertNotIn("PR_NUMBER:", workflow)
        self.assertIn("MANUAL_CHANGELOG: ${{ inputs.changelog }}", workflow)
        self.assertIn("permissions:\n  actions: read", workflow)
        self.assertIn(
            "actions/create-github-app-token@bcd2ba49218906704ab6c1aa796996da409d3eb1 # v3.2.0",
            workflow,
        )
        self.assertIn("actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2", workflow)
        self.assertIn(
            "actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065 # v5.6.0",
            workflow,
        )
        self.assertNotRegex(
            workflow,
            r"uses: actions/(?:create-github-app-token|checkout|setup-python)@v",
        )
        self.assertIn("client-id: ${{ vars.CHANGELOG_APP_CLIENT_ID }}", workflow)
        self.assertIn("private-key: ${{ secrets.CHANGELOG_APP_PRIVATE_KEY }}", workflow)
        self.assertIn("permission-contents: write", workflow)
        self.assertIn("permission-pull-requests: read", workflow)
        self.assertIn("token: ${{ steps.app-token.outputs.token }}", workflow)
        self.assertIn("ACTIONS_TOKEN: ${{ github.token }}", workflow)
        self.assertIn("CHANGELOG_FILE: ${{ vars.CHANGELOG_FILE }}", workflow)
        self.assertNotIn("actions_changelogs_since_last_run.py", publish_workflow)
        self.assertIn('workflows: ["Publish Stable"]', dispatch_workflow)
        self.assertIn("branches: [stable]", dispatch_workflow)
        self.assertIn("github.event.workflow_run.conclusion == 'success'", dispatch_workflow)
        self.assertIn("actions: write", dispatch_workflow)
        self.assertIn(
            "SOURCE_WORKFLOW_RUN_ID: ${{ github.event.workflow_run.id || inputs.source_workflow_run_id }}",
            dispatch_workflow,
        )
        self.assertIn("dispatch_changelogs.py", dispatch_workflow)
        self.assertIn("workflow_dispatch:", discord_workflow)
        self.assertIn(
            "run-name: Discord changelog ${{ inputs.target_id }} for ${{ inputs.released_sha }}",
            discord_workflow,
        )
        self.assertIn("CHANGELOG_FILE: ${{ steps.target.outputs.changelog_file }}", discord_workflow)
        self.assertIn("DISCORD_WEBHOOK_URL: ${{ secrets[steps.target.outputs.webhook_secret] }}", discord_workflow)
        self.assertIn("GITHUB_TOKEN: ${{ github.token }}", discord_workflow)
        self.assertIn("group: publish-discord-changelog-${{ inputs.target_id }}", discord_workflow)
        self.assertIn("persist-credentials: false", discord_workflow)
        self.assertIn("changelog_targets.py", discord_workflow)
        self.assertIn("CHANGELOG_TARGET_ID: ${{ inputs.target_id }}", discord_workflow)
        self.assertIn('changelog_targets.py "$CHANGELOG_TARGET_ID"', discord_workflow)
        self.assertNotIn('changelog_targets.py "${{ inputs.target_id }}"', discord_workflow)
        self.assertIn("actions_changelogs_since_last_run.py", discord_workflow)
        self.assertNotIn("CHANGELOG_TOKEN", workflow)
        self.assertNotIn("CHANGELOG_SSH_KEY", workflow)
        self.assertIn("concurrency:", workflow)
        self.assertIn("cancel-in-progress: false", workflow)
        self.assertIn("run: bash Tools/_sunrise/changelog/run.sh", workflow)
        self.assertNotIn("git reset --hard origin/master", workflow)
        self.assertIn("git reset --hard origin/master", runner)
        self.assertIn("for attempt in {1..5}", runner)
        self.assertIn("python Tools/_sunrise/changelog/changelog_actions.py", runner)
        self.assertIn('arguments+=(--manual-changelog)', runner)

        self.assertIn('if [[ -z "${MANUAL_CHANGELOG:-}" ]]', runner)
        self.assertNotIn("changelog-state.json", runner)
        self.assertIn('commit_message_file="$(mktemp)"', runner)
        self.assertIn('git commit -F "$commit_message_file"', runner)
        self.assertIn('if [[ -z "${CHANGELOG_FILE:-}" ]]', runner)
        self.assertIn('changelog_directory="$(dirname -- "$CHANGELOG_FILE")"', runner)
        self.assertIn('"$category" == *".."*', runner)
        self.assertIn('! "$category" =~ ^[A-Za-z]+$', runner)
        self.assertIn('changelog_files+=("$changelog_directory/$category.yml")', runner)
        self.assertIn('git add -- "${changelog_files[@]}"', runner)
        self.assertIn("git add -A -- Resources/Changelog/Parts", runner)
        self.assertNotIn('git add -- "$changelog_directory"', runner)
        self.assertIn("Чейнджлог уже актуален: публикация не требуется.", runner)
        self.assertIn("Чейнджлог успешно опубликован в master.", runner)
        self.assertIn("Не удалось отправить чейнджлог после пяти попыток.", runner)
        self.assertNotIn("github.event.pull_request.head", workflow)

    def test_validation_workflow_uses_trusted_parser_and_single_check_run(self):
        workflow = VALIDATE_WORKFLOW_PATH.read_text(encoding="utf-8")
        document = yaml.load(workflow, Loader=yaml.BaseLoader)

        self.assertIn("on", document)
        self.assertIn("jobs", document)
        self.assertIn(
            "types: [opened, edited, reopened, synchronize, ready_for_review]",
            workflow,
        )
        self.assertNotIn("statuses: write", workflow)
        self.assertIn("pull-requests: read", workflow)
        self.assertIn("group: validate-changelog-${{ github.event.pull_request.number }}", workflow)
        self.assertIn("cancel-in-progress: true", workflow)
        self.assertIn("ref: ${{ github.event.pull_request.base.ref }}", workflow)
        self.assertNotIn("ref: ${{ github.event.pull_request.head.sha }}", workflow)
        self.assertNotIn("github.event.pull_request.body", workflow)
        self.assertIn("persist-credentials: false", workflow)
        self.assertIn("name: Validate Changelog", workflow)
        self.assertNotIn("HEAD_SHA: ${{ github.event.pull_request.head.sha }}", workflow)
        self.assertNotIn("context=changelog/parse", workflow)
        self.assertNotIn("-f state=pending", workflow)
        self.assertIn("--validate-only", workflow)
        self.assertIn("GITHUB_TOKEN: ${{ github.token }}", workflow)
        self.assertNotIn("continue-on-error: true", workflow)
        self.assertNotIn("steps.parse.outcome != 'success'", workflow)

    def test_parts_staging_includes_new_file(self):
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            changelog = repo_root / "Resources/Changelog/ChangelogSunrise.yml"
            part = repo_root / "Resources/Changelog/Parts/new.yml"
            changelog.parent.mkdir(parents=True)
            part.parent.mkdir(parents=True)
            changelog.write_text("Entries: []\n", encoding="utf-8")
            part.write_text("changes: []\n", encoding="utf-8")

            subprocess.run(["git", "init", "--quiet"], cwd=repo_root, check=True)
            subprocess.run(
                ["git", "add", "--", "Resources/Changelog/ChangelogSunrise.yml"],
                cwd=repo_root,
                check=True,
            )
            subprocess.run(
                ["git", "add", "-A", "--", "Resources/Changelog/Parts"],
                cwd=repo_root,
                check=True,
            )
            staged = subprocess.run(
                ["git", "diff", "--cached", "--name-only"],
                cwd=repo_root,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.splitlines()

        self.assertEqual(
            [
                "Resources/Changelog/ChangelogSunrise.yml",
                "Resources/Changelog/Parts/new.yml",
            ],
            staged,
        )

    def test_schema_validator_and_repair_share_issue_messages(self):
        document = {
            "Entries": [
                {
                    "author": "Fish",
                    "time": "2026-08-16T10:30:08+00:00",
                    "changes": [{"type": "Add", "message": "Карта", "id": 21}],
                },
                {
                    "author": "Sunrise",
                    "time": "2026-08-16T11:30:08+00:00",
                    "changes": [{"type": "Fix", "message": "Исправление"}],
                    "id": 7,
                },
                {
                    "author": "Duplicate",
                    "time": "2026-08-16T12:30:08+00:00",
                    "changes": [{"type": "Tweak", "message": "Повтор"}],
                    "id": 7,
                },
            ],
        }

        issues = changelog_schema.inspect_changelog_document(document)
        repairs = changelog_schema.repair_changelog_document(document)

        self.assertEqual(
            [issue.message for issue in issues],
            [repair.issue.message for repair in repairs],
        )
        self.assertEqual([7, 8, 9], sorted(entry["id"] for entry in document["Entries"]))
        self.assertNotIn("id", document["Entries"][0]["changes"][0])

    def test_commit_message_describes_repairs_and_keeps_normal_subject(self):
        normal = changelog_actions.build_commit_message(
            [{"file": "Resources/Changelog/ChangelogSunrise.yml", "added": 1, "repairs": []}],
        )
        repaired = changelog_actions.build_commit_message(
            [
                {
                    "file": "Resources/Changelog/ChangelogFish.yml",
                    "added": 2,
                    "repairs": [
                        {
                            "code": "nested_id",
                            "message": "Запись #1: ошибка id.",
                            "resolution": "Поле id удалено.",
                        },
                    ],
                },
            ],
        )

        self.assertEqual("Automatic changelog update [skip ci]\n", normal)
        self.assertTrue(repaired.startswith("Automatic changelog update + fix [skip ci]\n\n"))
        self.assertIn("### Что исправлено в `Resources/Changelog/ChangelogFish.yml`", repaired)
        self.assertIn("1. Проблема: Запись #1: ошибка id.", repaired)
        self.assertIn("Решение: Поле id удалено.", repaired)
        self.assertIn("### Почему это было необходимо", repaired)

    def test_pr_file_validation_reuses_schema_diagnostic(self):
        document = """Entries:
- author: Fish
  time: '2026-08-16T10:30:08+00:00'
  changes:
  - type: Add
    message: Карта
    id: 21
"""
        expected = (
            "Запись #1, изменение #1: поле id находится внутри changes; "
            "id должен быть полем записи."
        )

        with tempfile.TemporaryDirectory() as directory:
            summary = Path(directory) / "summary.md"
            with patch.dict(
                "os.environ",
                {
                    "GITHUB_TOKEN": "token",
                    "GITHUB_REPOSITORY": "makura-games/fish-station",
                    "GITHUB_STEP_SUMMARY": str(summary),
                },
            ), patch.object(
                changelog_actions,
                "list_changed_changelog_paths",
                return_value=["Resources/Changelog/ChangelogFish.yml"],
            ), patch.object(
                changelog_actions,
                "load_repository_file",
                return_value=document,
            ):
                with self.assertRaisesRegex(RuntimeError, "не прошёл проверку структуры"):
                    changelog_actions.validate_changed_changelog_files(
                        {"number": 123, "head": {"sha": "a" * 40}},
                    )

            report = summary.read_text(encoding="utf-8")

        self.assertIn(expected, report)
        self.assertIn("Перенесите id на уровень записи", report)

    def test_changed_changelog_filter_includes_configured_category_files(self):
        files = [
            {"filename": "Resources/Changelog/ChangelogSunrise.yml"},
            {"filename": "Resources/Changelog/Admin.yml"},
            {"filename": "Resources/Changelog/Fish.yml"},
            {"filename": "Resources/Changelog/Other.yml"},
            {"filename": "Resources/Changelog/Nested/Admin.yml"},
            {"filename": "Resources/Changelog/Admin.txt"},
        ]
        with patch.dict(
            "os.environ",
            {
                "GITHUB_REPOSITORY": "makura-games/sunrise-station",
                "CHANGELOG_EXTRA_CATEGORIES": "Admin, Fish",
            },
        ), patch.object(changelog_actions, "github_request", return_value=files):
            paths = changelog_actions.list_changed_changelog_paths(123)

        self.assertEqual(
            [
                "Resources/Changelog/Admin.yml",
                "Resources/Changelog/ChangelogSunrise.yml",
                "Resources/Changelog/Fish.yml",
            ],
            paths,
        )

    def test_large_repository_file_falls_back_to_git_blob(self):
        text = "Entries: []\n# Рыба\n"
        contents = {"encoding": "none", "content": "", "sha": "blob-sha"}
        blob = {
            "encoding": "base64",
            "content": base64.b64encode(text.encode("utf-8")).decode("ascii"),
        }
        with patch.dict("os.environ", {"GITHUB_REPOSITORY": "makura-games/fish-station"}), patch.object(
            changelog_actions,
            "github_request",
            side_effect=[contents, blob],
        ) as request:
            result = changelog_actions.load_repository_file(
                "Resources/Changelog/ChangelogFish.yml",
                "a" * 40,
            )

        self.assertEqual(text, result)
        self.assertEqual(
            call("/repos/makura-games/fish-station/git/blobs/blob-sha"),
            request.call_args_list[1],
        )

    def test_yaml_error_without_problem_is_reported(self):
        with tempfile.TemporaryDirectory() as directory:
            summary = Path(directory) / "summary.md"
            with patch.dict(
                "os.environ",
                {
                    "GITHUB_TOKEN": "token",
                    "GITHUB_REPOSITORY": "makura-games/fish-station",
                    "GITHUB_STEP_SUMMARY": str(summary),
                },
            ), patch.object(
                changelog_actions,
                "list_changed_changelog_paths",
                return_value=["Resources/Changelog/ChangelogFish.yml"],
            ), patch.object(
                changelog_actions,
                "load_repository_file",
                return_value="broken",
            ), patch.object(
                changelog_actions.yaml,
                "safe_load",
                side_effect=yaml.YAMLError("неверная кодировка"),
            ):
                with self.assertRaisesRegex(RuntimeError, "не прошёл проверку структуры"):
                    changelog_actions.validate_changed_changelog_files(
                        {"number": 123, "head": {"sha": "a" * 40}},
                    )

            report = summary.read_text(encoding="utf-8")

        self.assertIn("неверная кодировка", report)

    def test_discord_diff_falls_back_for_fish_ids_without_losing_entries(self):
        old = {
            "Entries": [
                {
                    "author": "Old",
                    "time": "2026-08-16T10:30:08+00:00",
                    "changes": [{"type": "Add", "message": "Карта", "id": 21}],
                },
                {
                    "author": "Existing",
                    "time": "2026-08-16T11:30:08+00:00",
                    "changes": [{"type": "Fix", "message": "Старое"}],
                    "id": 21,
                },
            ],
        }
        current = {
            "Entries": [
                {
                    "author": "Old",
                    "time": "2026-08-20T10:30:08+00:00",
                    "changes": [{"type": "Add", "message": "Карта"}],
                    "id": 27,
                },
                {
                    "author": "New",
                    "time": "2026-08-20T11:30:08+00:00",
                    "changes": [{"type": "Fix", "message": "Новое"}],
                    "id": 21,
                },
            ],
        }

        diff = list(discord_changelog.diff_changelog(old, current))

        self.assertEqual(["New"], [entry["author"] for entry in diff])

    def test_earliest_failed_target_attempt_is_used_for_bootstrap(self):
        current = {
            "id": 300,
            "created_at": "2026-08-25T22:39:08Z",
            "display_title": f"Discord changelog fish for {'d' * 40}",
        }
        past = {
            "workflow_runs": [
                {
                    "id": 200,
                    "created_at": "2026-08-18T23:03:34Z",
                    "display_title": f"Discord changelog fish for {'b' * 40}",
                },
                {
                    "id": 210,
                    "created_at": "2026-08-18T23:03:36Z",
                    "display_title": f"Discord changelog sunrise for {'b' * 40}",
                },
                {
                    "id": 220,
                    "created_at": "2026-08-18T23:15:39Z",
                    "display_title": f"Discord changelog fish  for {'c' * 40}",
                },
            ],
        }

        with patch.object(discord_changelog, "get_current_run", return_value=current), patch.object(
            discord_changelog,
            "get_past_runs",
            return_value=past,
        ):
            result = discord_changelog.get_earliest_target_attempt(
                Mock(),
                "makura-games/fish-station",
                "300",
                "fish",
            )

        self.assertEqual(200, result["id"])

    def test_source_release_before_first_failed_attempt_stays_fixed(self):
        first_sha = "b" * 40
        first_attempt = {
            "created_at": "2026-08-18T23:03:34Z",
            "display_title": f"Discord changelog fish for {first_sha}",
        }
        source_run = {"workflow_url": "https://api.github.test/workflows/stable"}
        source_history = {
            "workflow_runs": [
                {
                    "id": 200,
                    "created_at": "2026-08-18T22:50:00Z",
                    "display_title": f"Publish Stable {first_sha}",
                    "head_commit": {"id": "c" * 40},
                },
                {
                    "id": 100,
                    "created_at": "2026-08-14T22:00:00Z",
                    "head_commit": {"id": "a" * 40},
                },
            ],
        }

        with patch.object(discord_changelog, "get_current_run", return_value=source_run), patch.object(
            discord_changelog,
            "get_past_runs",
            return_value=source_history,
        ):
            result = discord_changelog.get_source_release_before_attempt(
                Mock(),
                "makura-games/fish-station",
                "200",
                first_attempt,
            )

        self.assertEqual(100, result["id"])

    def test_source_release_falls_back_when_first_sha_is_absent(self):
        first_attempt = {
            "created_at": "2026-08-18T23:03:34Z",
            "display_title": f"Discord changelog fish for {'b' * 40}",
        }
        source_run = {"workflow_url": "https://api.github.test/workflows/stable"}
        latest_before_attempt = {
            "id": 200,
            "created_at": "2026-08-18T22:50:00Z",
            "head_commit": {"id": "c" * 40},
        }
        incomplete_run = {
            "id": 300,
            "created_at": "2026-08-18T22:55:00Z",
            "head_commit": None,
        }

        with patch.object(discord_changelog, "get_current_run", return_value=source_run), patch.object(
            discord_changelog,
            "get_past_runs",
            return_value={"workflow_runs": [incomplete_run, latest_before_attempt]},
        ):
            result = discord_changelog.get_source_release_before_attempt(
                Mock(),
                "makura-games/fish-station",
                "200",
                first_attempt,
            )

        self.assertEqual(200, result["id"])

    def test_target_id_trims_manual_whitespace(self):
        self.assertEqual("fish", changelog_targets.validate_target_id(" fish "))


if __name__ == "__main__":
    unittest.main()
