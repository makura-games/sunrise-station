"""BYOND .dmi spritesheet metadata (zTXt Description) and frame SHA256 inspector."""

from __future__ import annotations

import hashlib
import warnings
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image

from .config import safe_resolve_under_repo, to_posix_rel


@dataclass(slots=True)
class DmiStateMeta:
    """Parsed metadata for a single state inside a .dmi file."""

    name: str
    dirs: int
    frames: int
    delay: list[float] | None
    loop: int | None
    rewind: bool
    movement: bool
    hotspot: str | None
    tile_start: int
    tile_count: int


@dataclass(slots=True)
class ParsedDmi:
    """Parsed .dmi header and state list."""

    version: str
    width: int
    height: int
    states: list[DmiStateMeta]


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _dir_labels(dirs: int) -> list[str]:
    if dirs == 1:
        return ["south"]
    if dirs == 4:
        return ["south", "north", "east", "west"]
    if dirs == 8:
        return [
            "south",
            "north",
            "east",
            "west",
            "southeast",
            "southwest",
            "northeast",
            "northwest",
        ]
    return [f"dir_{i}" for i in range(dirs)]


def _unquote_dmi_str(val: str) -> str:
    s = val.strip()
    if len(s) >= 2 and s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    return s.replace('\\"', '"').replace("\\\\", "\\")


def parse_dmi_description(description: str) -> ParsedDmi:
    """Parse BYOND DMI zTXt Description text into version, dimensions, and states."""
    version = ""
    width = 32
    height = 32
    states: list[DmiStateMeta] = []

    current: dict[str, Any] | None = None
    tile_cursor = 0

    def _flush_state() -> None:
        nonlocal current, tile_cursor
        if current is None:
            return
        dirs = int(current.get("dirs", 1))
        frames = int(current.get("frames", 1))
        tile_count = max(0, dirs * frames)
        states.append(
            DmiStateMeta(
                name=current["name"],
                dirs=dirs,
                frames=frames,
                delay=current.get("delay"),
                loop=current.get("loop"),
                rewind=bool(current.get("rewind", False)),
                movement=bool(current.get("movement", False)),
                hotspot=current.get("hotspot"),
                tile_start=tile_cursor,
                tile_count=tile_count,
            )
        )
        tile_cursor += tile_count
        current = None

    for raw_line in description.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        key, raw_val = line.split("=", 1)
        key = key.strip()
        val = raw_val.strip()

        if key == "version" and current is None:
            version = val
        elif key == "width" and current is None:
            width = int(val)
        elif key == "height" and current is None:
            height = int(val)
        elif key == "state":
            _flush_state()
            current = {"name": _unquote_dmi_str(val)}
        elif current is not None:
            if key == "dirs":
                current["dirs"] = int(val)
            elif key == "frames":
                current["frames"] = int(val)
            elif key == "delay":
                delays: list[float] = []
                for part in val.split(","):
                    part = part.strip()
                    if part:
                        try:
                            delays.append(float(part))
                        except ValueError:
                            pass
                current["delay"] = delays
            elif key == "loop":
                try:
                    current["loop"] = int(val)
                except ValueError:
                    pass
            elif key == "rewind":
                current["rewind"] = val in ("1", "true", "True")
            elif key == "movement":
                current["movement"] = val in ("1", "true", "True")
            elif key == "hotspot":
                current["hotspot"] = val

    _flush_state()
    return ParsedDmi(version=version, width=width, height=height, states=states)


def _resolve_dmi_path(repo_root: Path, dmi_path: str) -> Path:
    cleaned = dmi_path.strip().replace("\\", "/").lstrip("/")
    if not cleaned:
        raise ValueError("dmi_path must not be empty.")

    candidates = [cleaned]
    if not cleaned.lower().startswith("icons/"):
        candidates.append(f"icons/{cleaned}")

    for cand in candidates:
        p = safe_resolve_under_repo(repo_root, cand)
        if p.is_file():
            return p

    # Return primary resolved path (still checked for traversal) for error reporting
    return safe_resolve_under_repo(repo_root, cleaned)


class DmiInspector:
    """mtime-cached DMI metadata and frame-hash inspector."""

    def __init__(self, repo_root: Path, repo_name: str = "default") -> None:
        self.repo_root = repo_root
        self.repo_name = repo_name
        self._cache: dict[str, tuple[tuple[int, int], dict[str, Any]]] = {}

    def inspect(
        self,
        dmi_path: str,
        state: str | None = None,
        include_frame_hashes: bool = False,
    ) -> dict[str, Any]:
        """Inspect a .dmi file's zTXt metadata and optionally compute frame/state SHA256."""
        resolved = _resolve_dmi_path(self.repo_root, dmi_path)
        rel_path = to_posix_rel(resolved, self.repo_root)
        if not resolved.is_file():
            return {
                "error": f"DMI file not found: '{dmi_path}' (resolved '{rel_path}').",
                "repo": self.repo_name,
                "dmi_path": rel_path,
            }

        st = resolved.stat()
        sig = (st.st_mtime_ns, st.st_size)
        cache_key = f"{rel_path}|{state or ''}|{int(include_frame_hashes)}"
        cached = self._cache.get(cache_key)
        if cached is not None and cached[0] == sig:
            return cached[1]

        with warnings.catch_warnings():
            warnings.simplefilter("ignore", Image.DecompressionBombWarning)
            with Image.open(resolved) as img:
                img_width, img_height = img.size
                description = img.info.get("Description")
                if not isinstance(description, str) or not description.strip():
                    return {
                        "error": f"No DMI 'Description' zTXt metadata found in '{rel_path}'.",
                        "repo": self.repo_name,
                        "dmi_path": rel_path,
                    }

                parsed = parse_dmi_description(description)
                selected_states = parsed.states
                if state is not None:
                    selected_states = [s for s in parsed.states if s.name == state]
                    if not selected_states:
                        sample = [s.name for s in parsed.states[:25]]
                        return {
                            "error": f"State '{state}' not found in '{rel_path}'.",
                            "repo": self.repo_name,
                            "dmi_path": rel_path,
                            "available_states_count": len(parsed.states),
                            "available_states_sample": sample,
                        }

                rgba = img.convert("RGBA") if include_frame_hashes else None

        tile_w = parsed.width
        tile_h = parsed.height
        cols = img_width // tile_w if tile_w > 0 else 0
        rows = img_height // tile_h if tile_h > 0 else 0
        total_grid_slots = cols * rows

        states_out: list[dict[str, Any]] = []
        for st_meta in selected_states:
            entry: dict[str, Any] = {
                "name": st_meta.name,
                "dirs": st_meta.dirs,
                "frames": st_meta.frames,
                "delay": st_meta.delay,
                "loop": st_meta.loop,
                "rewind": st_meta.rewind,
                "movement": st_meta.movement,
            }
            if st_meta.hotspot is not None:
                entry["hotspot"] = st_meta.hotspot

            if include_frame_hashes and rgba is not None and cols > 0:
                if st_meta.tile_start + st_meta.tile_count > total_grid_slots:
                    entry["hash_error"] = (
                        f"State tiles ({st_meta.tile_start}..{st_meta.tile_start + st_meta.tile_count}) "
                        f"exceed grid capacity ({total_grid_slots})."
                    )
                else:
                    labels = _dir_labels(st_meta.dirs)
                    frame_hashes: dict[str, list[str]] = {lbl: [] for lbl in labels}
                    state_hasher = hashlib.sha256()
                    all_hashes: list[str] = []

                    # DMI spritesheets store tiles in frame-major x dir order
                    for f_idx in range(st_meta.frames):
                        for d_idx in range(st_meta.dirs):
                            tile_idx = st_meta.tile_start + f_idx * st_meta.dirs + d_idx
                            col = tile_idx % cols
                            row = tile_idx // cols
                            box = (
                                col * tile_w,
                                row * tile_h,
                                (col + 1) * tile_w,
                                (row + 1) * tile_h,
                            )
                            tile_bytes = rgba.crop(box).tobytes()
                            state_hasher.update(tile_bytes)
                            h = _sha256_bytes(tile_bytes)
                            frame_hashes[labels[d_idx]].append(h)
                            all_hashes.append(h)

                    entry["state_sha256"] = state_hasher.hexdigest()
                    entry["frame_hashes"] = frame_hashes
                    entry["unique_frame_hashes"] = len(set(all_hashes))
                    entry["all_frames_identical"] = st_meta.frames > 1 and all(
                        len(set(hashes)) == 1 for hashes in frame_hashes.values()
                    )

            states_out.append(entry)

        result: dict[str, Any] = {
            "repo": self.repo_name,
            "dmi_path": rel_path,
            "version": parsed.version,
            "width": tile_w,
            "height": tile_h,
            "image_size": {"width": img_width, "height": img_height},
            "states_total": len(parsed.states),
            "states_returned": len(states_out),
            "states": states_out,
        }
        self._cache[cache_key] = (sig, result)
        return result
