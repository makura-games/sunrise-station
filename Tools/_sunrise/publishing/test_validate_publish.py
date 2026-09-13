import importlib.util
import io
import os
import sys
import unittest
from pathlib import Path
from contextlib import redirect_stdout
from unittest.mock import Mock, mock_open, patch

from validate_publish import main, validate_cdn_url


class PublishTests(unittest.TestCase):
    def test_cdn_validation_and_normalization(self):
        self.assertEqual(validate_cdn_url(" https://cdn.example/base/// "), "https://cdn.example/base/")
        self.assertEqual(validate_cdn_url("https://[::1]:443"), "https://[::1]:443/")
        for value in (
            "", "http://cdn.example", "https:///path", "https://user@cdn.example",
            "https://cdn.example?q=1", "https://cdn.example#fragment",
            "https://cdn.example:invalid", "https://cdn.example:65536", "https://[broken",
            "https://cdn.example/white space", "https://cdn.example/line\nbreak",
        ):
            with self.subTest(value=value), self.assertRaises(ValueError):
                validate_cdn_url(value)

    def test_configuration_fails_before_build(self):
        environment = {
            "ROBUST_CDN_URL": "https://cdn.example", "PUBLISH_FORK_ID": "test",
            "CURRENT_REF_NAME": "master", "CURRENT_REF_TYPE": "branch",
        }
        with patch.dict(os.environ, environment, clear=True):
            main()
            with patch.dict(os.environ, {"COMMIT_HASH": "a" * 40}):
                main()
            for invalid in (
                {"ROBUST_CDN_URL": "http://cdn.example"}, {"PUBLISH_FORK_ID": " "},
                {"COMMIT_HASH": "master"}, {"COMMIT_HASH": "abc123"},
                {"PRIVATE_COMMIT_HASH": "refs/pull/1/head"},
                {"CURRENT_REF_TYPE": "tag"}, {"CURRENT_REF_NAME": "feature"},
                {"USE_PRIVATE_CONTENT": "true"},
                {"USE_PRIVATE_CONTENT": "true", "PRIVATE_REPOSITORY": "org/private"},
            ):
                with self.subTest(invalid=invalid), patch.dict(os.environ, invalid), self.assertRaises(SystemExit):
                    main()

    def test_publish_uses_checked_out_sha_and_current_configuration(self):
        tools_path = Path(__file__).resolve().parent
        spec = importlib.util.spec_from_file_location("publish_under_test", tools_path / "publish_multi_request.py")
        module = importlib.util.module_from_spec(spec)
        session = Mock()
        selected_sha = "a" * 40
        environment = {
            "PUBLISH_TOKEN": "test-token", "ROBUST_CDN_URL": " https://cdn.example/base/ ",
            "GITHUB_SHA": "b" * 40,
        }
        with patch.dict(os.environ, environment), patch.object(sys, "path", [str(tools_path), *sys.path]), patch(
            "subprocess.check_output", return_value=selected_sha + "\n"
        ) as git:
            spec.loader.exec_module(module)
            git.assert_called_once_with(["git", "rev-parse", "HEAD"], encoding="UTF-8")

        announce_spec = importlib.util.spec_from_file_location(
            "announce_under_test", tools_path.parent / "discord_publish_announce.py"
        )
        announce = importlib.util.module_from_spec(announce_spec)
        with patch.dict(os.environ, environment), patch("subprocess.check_output", return_value=selected_sha + "\n"):
            announce_spec.loader.exec_module(announce)
        self.assertEqual(announce.VERSION, selected_sha)

        with patch.object(sys, "argv", ["publish", "--fork-id", " test "]), patch.object(
            module.requests, "Session", return_value=session
        ), patch.object(module, "get_files_to_publish", return_value=["release/client.zip"]), patch.object(
            module, "get_engine_version", return_value="1.0"
        ), patch("builtins.open", mock_open(read_data=b"archive")):
            module.main()

        self.assertEqual(session.post.call_count, 3)
        self.assertEqual(session.post.call_args_list[0].args[0], "https://cdn.example/base/fork/test/publish/start")
        self.assertEqual(session.post.call_args_list[0].kwargs["json"]["version"], selected_sha)
        self.assertEqual(session.post.call_args_list[1].kwargs["headers"]["Robust-Cdn-Publish-Version"], selected_sha)
        self.assertEqual(session.post.call_args_list[2].kwargs["json"]["version"], selected_sha)
        self.assertEqual([call.kwargs["timeout"] for call in session.post.call_args_list], [(15, 60), (15, 600), (15, 60)])

    def test_artifact_download_url_is_sent_but_not_logged(self):
        path = Path(__file__).resolve().parent / "publish_github_artifact.py"
        spec = importlib.util.spec_from_file_location("artifact_under_test", path)
        module = importlib.util.module_from_spec(spec)
        environment = {
            "GITHUB_TOKEN": "github-token", "PUBLISH_TOKEN": "publish-token",
            "ARTIFACT_ID": "123", "GITHUB_REPOSITORY": "org/repo", "GITHUB_SHA": "a" * 40,
            "ROBUST_CDN_URL": "https://cdn.example", "PUBLISH_FORK_ID": "test",
        }
        with patch.dict(os.environ, environment):
            spec.loader.exec_module(module)

        artifact_url = "https://downloads.example/archive.zip?signature=private-signature"
        output = io.StringIO()
        with patch.object(module, "get_artifact_url", return_value=artifact_url), patch.object(
            module, "get_engine_version", return_value="1.0"
        ), patch.object(module.requests, "post") as post, redirect_stdout(output):
            module.main()

        self.assertIn("Publishing artifact 123", output.getvalue())
        self.assertNotIn(artifact_url, output.getvalue())
        self.assertNotIn("private-signature", output.getvalue())
        self.assertEqual(post.call_args.kwargs["json"]["archive"], artifact_url)


if __name__ == "__main__":
    unittest.main()
