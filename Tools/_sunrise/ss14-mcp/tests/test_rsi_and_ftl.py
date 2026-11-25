"""Unit and smoke tests for RSI/attributions auditor and FTL auditor."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

from ss14_mcp.config import find_repo_root
from ss14_mcp.ftl_audit import FtlIndex, audit_ftl
from ss14_mcp.proto_index import ProtoIndex
from ss14_mcp.rsi_audit import audit_rsi


def _make_mini_repo(tmp_path: Path) -> Path:
    (tmp_path / "Resources" / "Prototypes").mkdir(parents=True)
    (tmp_path / "Resources" / "Textures" / "Test").mkdir(parents=True)
    (tmp_path / "Resources" / "Audio" / "Test").mkdir(parents=True)
    (tmp_path / "Resources" / "Locale" / "ru-RU" / "_sunrise").mkdir(parents=True)
    (tmp_path / "Resources" / "Locale" / "en-US" / "_Sunrise").mkdir(parents=True)
    (tmp_path / "Content.Server").mkdir(parents=True)
    (tmp_path / "Content.Shared" / "Test").mkdir(parents=True)
    return tmp_path


def test_rsi_audit_valid_and_duplicates(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    rsi = repo / "Resources" / "Textures" / "Test" / "sample.rsi"
    rsi.mkdir()

    meta = {
        "version": 1,
        "license": "CC-BY-SA-4.0",
        "copyright": "Test Author",
        "size": {"x": 32, "y": 32},
        "states": [
            {"name": "icon"},
            {"name": "dup"},
            {"name": "anim", "delays": [[0.1, 0.1]]},
        ],
    }
    (rsi / "meta.json").write_text(json.dumps(meta), encoding="utf-8")

    img1 = Image.new("RGBA", (32, 32), (255, 0, 0, 255))
    img1.save(rsi / "icon.png")
    img1.save(rsi / "dup.png")  # byte-identical duplicate

    # Animated 2-frame PNG with identical frames -> placeholder warning
    anim_img = Image.new("RGBA", (64, 32), (0, 255, 0, 255))
    anim_img.save(rsi / "anim.png")

    # Audio + comma-separated attributions.yml
    audio_file = repo / "Resources" / "Audio" / "Test" / "beep.ogg"
    audio_file.write_bytes(b"OggS")
    (repo / "Resources" / "Audio" / "Test" / "attributions.yml").write_text(
        '- files: ["beep.ogg,\n  other.ogg"]\n  license: "CC0-1.0"\n  copyright: "Test"\n',
        encoding="utf-8",
    )

    res = audit_rsi(
        repo,
        "Resources/Textures/Test/sample.rsi",
        check_attributions=True,
        audio_paths=["/Audio/Test/beep.ogg"],
    )
    assert res["ok"] is False
    assert res["states_in_meta"] == 3
    assert res["png_on_disk"] == 3
    assert "states" not in res
    assert len(res["duplicate_png_groups"]) == 1
    assert any("pixel-identical" in i for i in res["issues"])
    assert res["audio"][0]["exists"] is True
    assert res["audio"][0]["attributed"] is True

    # verbose=True includes full states list
    res_verbose = audit_rsi(repo, "Resources/Textures/Test/sample.rsi", verbose=True)
    assert len(res_verbose["states"]) == 3

    # Audio directory auto-detection via rsi_path and explicit audio_dir
    audio_dir_res = audit_rsi(repo, "Resources/Audio/Test")
    assert audio_dir_res["audio_files_count"] == 1
    assert audio_dir_res["attributed_count"] == 1
    assert audio_dir_res["ok"] is True


def test_ftl_audit_parity_proto_and_cs(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    (repo / "Resources" / "Locale" / "ru-RU" / "_sunrise" / "clockwork-cult").mkdir(parents=True)
    (repo / "Resources" / "Locale" / "en-US" / "_Sunrise" / "clockworkcult").mkdir(parents=True)
    (repo / "Resources" / "Locale" / "ru-RU" / "_sunrise" / "items.ftl").write_text(
        "ent-ItemA = Предмет А\nui-only-ru = Только RU\n",
        encoding="utf-8",
    )
    (repo / "Resources" / "Locale" / "en-US" / "_Sunrise" / "items.ftl").write_text(
        "ent-ItemA = Item A\nui-only-en = Only EN\n",
        encoding="utf-8",
    )
    (repo / "Resources" / "Locale" / "ru-RU" / "_sunrise" / "clockwork-cult" / "cw.ftl").write_text(
        "cw-key = Латунь\n",
        encoding="utf-8",
    )
    (repo / "Resources" / "Locale" / "en-US" / "_Sunrise" / "clockworkcult" / "cw.ftl").write_text(
        "cw-key = Brass\n",
        encoding="utf-8",
    )
    (repo / "Resources" / "Prototypes" / "test_items.yml").write_text(
        "- type: entity\n  id: ItemA\n- type: entity\n  id: ItemMissingLoc\n- type: entity\n  id: AbstractBase\n  abstract: true\n",
        encoding="utf-8",
    )
    (repo / "Content.Shared" / "Test" / "Foo.cs").write_text(
        'var a = Loc.GetString("ui-only-ru");\nvar b = Loc.GetString("missing-cs-key");\n',
        encoding="utf-8",
    )

    proto_idx = ProtoIndex(repo)
    ftl_idx = FtlIndex(repo)

    res = audit_ftl(
        repo,
        proto_idx,
        ftl_idx,
        path_prefix="_Sunrise",
        proto_path_prefix="test_items.yml",
        cs_path_prefix="Content.Shared/Test",
    )
    assert res["counts"]["missing_in_ru"] == 1
    assert res["counts"]["missing_in_en"] == 1
    assert res["prototype_check"]["missing_ent_in_ru_count"] == 1
    assert res["prototype_check"]["missing_ent_in_ru"][0]["id"] == "ItemMissingLoc"
    assert res["cs_check"]["missing_in_ru_count"] == 1
    assert res["cs_check"]["missing_in_en_count"] == 2

    # query matches both clockwork-cult and clockworkcult via -/_ normalization
    q_res = audit_ftl(repo, proto_idx, ftl_idx, query="clockwork")
    assert q_res["counts"]["ru_keys"] == 1
    assert q_res["counts"]["en_keys"] == 1
    assert q_res["counts"]["missing_in_ru"] == 0
    assert len(q_res["matched_dirs"]) == 2

    # Zero match on path_prefix returns matched_dirs suggestions
    miss_res = audit_ftl(repo, proto_idx, ftl_idx, path_prefix="_Sunrise/nonexistent/clockwork")
    assert "error" in miss_res
    assert len(miss_res["matched_dirs"]) >= 1


def test_real_repo_smoke_rsi_and_ftl() -> None:
    repo = find_repo_root()

    rsi_res = audit_rsi(
        repo,
        "_Sunrise/Abductor/Clothing/Back/Duffels/abductor.rsi",
        check_attributions=True,
        audio_paths=["/Audio/Ambience/anomaly_drone.ogg"],
    )
    assert "error" not in rsi_res
    assert rsi_res["states_in_meta"] == 4
    assert "states" not in rsi_res
    assert rsi_res["attributions"]["has_meta_attribution"] is True
    assert rsi_res["audio"][0]["exists"] is True
    assert rsi_res["audio"][0]["attributed"] is True

    anim_res = audit_rsi(
        repo,
        "Resources/Textures/_Sunrise/Abductor/Effects/teleportation.rsi",
        check_attributions=True,
    )
    assert "error" not in anim_res
    assert anim_res["states_in_meta"] == 2

    audio_dir_res = audit_rsi(repo, "Resources/Audio/Ambience")
    assert "error" not in audio_dir_res
    assert audio_dir_res["audio_files_count"] > 0

    ftl_res = audit_ftl(
        repo,
        ProtoIndex(repo),
        FtlIndex(repo),
        query="clockwork",
    )
    assert "error" not in ftl_res
    assert ftl_res["counts"]["ru_keys"] > 0
    assert len(ftl_res["matched_dirs"]) >= 1
