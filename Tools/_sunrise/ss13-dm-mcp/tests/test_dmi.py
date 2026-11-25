"""Unit tests and Paradise-master220 smoke tests for DMI inspection."""

from __future__ import annotations

import hashlib
import os
import time
from pathlib import Path

import pytest
from PIL import Image
from PIL.PngImagePlugin import PngInfo

from ss13_dm_mcp.dmi import DmiInspector

def _get_paradise_repo() -> Path | None:
    raw = os.environ.get("SS13_REPO_PATH", "").strip()
    if not raw:
        return None
    p = Path(raw).expanduser().resolve()
    if not (p / "icons" / "obj" / "clockwork.dmi").is_file():
        return None
    return p


_PARADISE_REPO = _get_paradise_repo()


def _make_synthetic_dmi(path: Path, *, duplicate_frames: bool = False) -> None:
    # 2 states:
    # - "idle": dirs=1, frames=1 (1 tile)
    # - "walk": dirs=4, frames=3, delay=1,2,1 (12 tiles)
    # Total 13 tiles in 4x4 grid of 8x8 tiles -> 32x32 image
    tile_w, tile_h = 8, 8
    cols, rows = 4, 4
    img = Image.new("RGBA", (cols * tile_w, rows * tile_h), (0, 0, 0, 0))

    def fill_tile(idx: int, color: tuple[int, int, int, int]) -> None:
        c = idx % cols
        r = idx // cols
        patch = Image.new("RGBA", (tile_w, tile_h), color)
        img.paste(patch, (c * tile_w, r * tile_h))

    fill_tile(0, (10, 20, 30, 255))  # idle
    for f in range(3):
        for d in range(4):
            idx = 1 + f * 4 + d
            if duplicate_frames:
                fill_tile(idx, (100, d * 40, 0, 255))
            else:
                fill_tile(idx, (100 + f * 30, d * 40, f + d, 255))

    desc = (
        "# BEGIN DMI\n"
        "version = 4.0\n"
        f"\twidth = {tile_w}\n"
        f"\theight = {tile_h}\n"
        'state = "idle"\n'
        "\tdirs = 1\n"
        "\tframes = 1\n"
        'state = "walk"\n'
        "\tdirs = 4\n"
        "\tframes = 3\n"
        "\tdelay = 1,2,1\n"
        "\tloop = 0\n"
        "\trewind = 1\n"
        "# END DMI\n"
    )
    meta = PngInfo()
    meta.add_text("Description", desc, zip=True)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "PNG", pnginfo=meta)


def test_dmi_ztxt_slicing_hashes_and_mtime(tmp_path: Path) -> None:
    dmi_file = tmp_path / "icons" / "mob" / "test.dmi"
    _make_synthetic_dmi(dmi_file, duplicate_frames=False)

    insp = DmiInspector(tmp_path, repo_name="test")
    res = insp.inspect("mob/test.dmi", state="walk", include_frame_hashes=True)
    assert res["version"] == "4.0"
    assert res["width"] == 8
    assert res["height"] == 8
    assert res["states_total"] == 2
    assert res["states_returned"] == 1

    st = res["states"][0]
    assert st["name"] == "walk"
    assert st["dirs"] == 4
    assert st["frames"] == 3
    assert st["delay"] == [1.0, 2.0, 1.0]
    assert st["loop"] == 0
    assert st["rewind"] is True
    assert st["unique_frame_hashes"] == 12
    assert st["all_frames_identical"] is False
    assert set(st["frame_hashes"].keys()) == {"south", "north", "east", "west"}
    assert len(st["frame_hashes"]["south"]) == 3

    # Verify exact frame-major x dir pixel hash for frame=1, dir=east (idx=2)
    expected_bytes = Image.new("RGBA", (8, 8), (130, 80, 3, 255)).tobytes()
    expected_sha = hashlib.sha256(expected_bytes).hexdigest()
    assert st["frame_hashes"]["east"][1] == expected_sha

    # Update file with duplicate frames -> mtime cache must refresh
    time.sleep(0.02)
    _make_synthetic_dmi(dmi_file, duplicate_frames=True)
    os.utime(dmi_file, None)
    res2 = insp.inspect("icons/mob/test.dmi", state="walk", include_frame_hashes=True)
    st2 = res2["states"][0]
    assert st2["all_frames_identical"] is True
    assert st2["unique_frame_hashes"] == 4

    # Verify None vs "" cache key separation
    empty_state_dmi = tmp_path / "icons" / "mob" / "empty_state.dmi"
    img2 = Image.new("RGBA", (16, 8), (1, 2, 3, 255))
    meta2 = PngInfo()
    meta2.add_text(
        "Description",
        '# BEGIN DMI\nversion = 4.0\n\twidth = 8\n\theight = 8\nstate = ""\n\tdirs = 1\n\tframes = 1\nstate = "walk"\n\tdirs = 1\n\tframes = 1\n# END DMI\n',
        zip=True,
    )
    img2.save(empty_state_dmi, "PNG", pnginfo=meta2)
    res_all = insp.inspect("icons/mob/empty_state.dmi", state=None)
    res_empty = insp.inspect("icons/mob/empty_state.dmi", state="")
    assert res_all["states_returned"] == 2
    assert res_empty["states_returned"] == 1
    assert res_empty["states"][0]["name"] == ""

    # Missing state & path traversal
    missing = insp.inspect("icons/mob/test.dmi", state="nonexistent")
    assert "error" in missing
    with pytest.raises(ValueError):
        insp.inspect("../../secret.dmi")


@pytest.mark.skipif(_PARADISE_REPO is None, reason="Set SS13_REPO_PATH to a Paradise build to run Paradise DMI smoke tests")
def test_paradise_clockwork_dmi_smoke() -> None:
    assert _PARADISE_REPO is not None
    insp = DmiInspector(_PARADISE_REPO, repo_name="paradise")
    res = insp.inspect("icons/obj/clockwork.dmi", state="clock_slab", include_frame_hashes=True)
    assert res["version"] == "4.0"
    assert res["width"] == 32
    assert res["height"] == 32
    assert res["states_returned"] == 1
    st = res["states"][0]
    assert st["name"] == "clock_slab"
    assert st["dirs"] == 1
    assert st["frames"] == 15
    assert len(st["state_sha256"]) == 64
    assert len(st["frame_hashes"]["south"]) == 15

    beacon = insp.inspect("icons/obj/clockwork.dmi", state="beacon", include_frame_hashes=True)
    bst = beacon["states"][0]
    assert bst["frames"] == 27
    assert len(bst["delay"]) == 27
    assert bst["delay"][0] == 0.5
    assert bst["delay"][-1] == 14.0
