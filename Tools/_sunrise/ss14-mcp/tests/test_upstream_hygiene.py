"""Unit and smoke tests for upstream_hygiene auditor."""

from __future__ import annotations

import subprocess
from pathlib import Path

from ss14_mcp.config import find_repo_root
from ss14_mcp.upstream_hygiene import check_upstream_hygiene


def _git(cwd: Path, *args: str) -> None:
    subprocess.run(
        ["git", *args],
        cwd=cwd,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def test_upstream_hygiene_markers_and_base_ref(tmp_path: Path) -> None:
    _git(tmp_path, "init")
    _git(tmp_path, "config", "user.email", "test@example.com")
    _git(tmp_path, "config", "user.name", "Tester")

    (tmp_path / "Content.Server").mkdir()
    (tmp_path / "Content.Server" / "Vanilla.cs").write_text("class Vanilla {}\n", encoding="utf-8")
    _git(tmp_path, "add", ".")
    _git(tmp_path, "commit", "-m", "initial")

    # 1. Edit inside _Sunrise -> allowed without markers
    (tmp_path / "Content.Server" / "_Sunrise").mkdir()
    (tmp_path / "Content.Server" / "_Sunrise" / "Feature.cs").write_text("class Feature {}\n", encoding="utf-8")

    # 2. Edit outside _Sunrise WITHOUT marker -> flagged
    (tmp_path / "Content.Server" / "Vanilla.cs").write_text(
        "class Vanilla { int x = 1; }\n", encoding="utf-8"
    )
    res_bad = check_upstream_hygiene(tmp_path)
    assert res_bad["ok"] is False
    assert res_bad["counts"]["sunrise"] == 1
    assert res_bad["counts"]["unmarked_outside_sunrise"] == 1

    # 3. Edit outside _Sunrise WITH AGENTS.md marker -> passes
    (tmp_path / "Content.Server" / "Vanilla.cs").write_text(
        "// Sunrise-Edit - добавлено поле\nclass Vanilla { int x = 1; }\n",
        encoding="utf-8",
    )
    res_ok = check_upstream_hygiene(tmp_path)
    assert res_ok["ok"] is True
    assert res_ok["counts"]["unmarked_outside_sunrise"] == 0

    # 4. Untracked non-code files (.md/.txt/.json) outside _Sunrise go to untracked_other and keep ok=True
    (tmp_path / "notes.md").write_text("# Scratch notes\n", encoding="utf-8")
    (tmp_path / "meta.json").write_text("{}\n", encoding="utf-8")
    res_md = check_upstream_hygiene(tmp_path)
    assert res_md["ok"] is True
    assert res_md["counts"]["untracked_other"] == 2
    assert set(res_md["untracked_other"]) == {"notes.md", "meta.json"}

    # 5. Invalid base_ref returns error
    res_err = check_upstream_hygiene(tmp_path, base_ref="nonexistent-ref-12345")
    assert "error" in res_err


def test_real_repo_smoke_hygiene() -> None:
    repo = find_repo_root()
    res = check_upstream_hygiene(repo)
    assert "error" not in res
    assert "counts" in res
