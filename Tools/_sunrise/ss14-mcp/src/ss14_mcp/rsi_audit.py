"""RSI sprite, audio directory, and attributions.yml auditor."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
from typing import Any

from PIL import Image

from .config import resolve_case_insensitive_path, safe_resolve_under_repo, to_posix_rel
from .yaml_loader import load_yaml_document

_AUDIO_EXTS = {".ogg", ".wav"}


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _resolve_asset_path(
    repo_root: Path, raw_path: str, default_prefix: str
) -> Path | None:
    """Resolve an asset path given as repo-relative, /Textures/..., /Audio/..., or _Sunrise/..."""
    cleaned = raw_path.strip().replace("\\", "/").lstrip("/")
    if not cleaned:
        return None

    candidates = [cleaned]
    if not cleaned.startswith("Resources/"):
        if cleaned.startswith("Textures/") or cleaned.startswith("Audio/"):
            candidates.append(f"Resources/{cleaned}")
        else:
            candidates.append(f"{default_prefix}/{cleaned}")
            other = "Resources/Audio" if default_prefix == "Resources/Textures" else "Resources/Textures"
            candidates.append(f"{other}/{cleaned}")

    for cand in candidates:
        try:
            direct = safe_resolve_under_repo(repo_root, cand)
            if direct.exists():
                return direct
        except ValueError:
            continue
        ci = resolve_case_insensitive_path(repo_root, cand)
        if ci is not None and ci.exists():
            try:
                ci.resolve().relative_to(repo_root.resolve())
            except ValueError:
                continue
            return ci
    return None


def _parse_attribution_files(raw_files: Any) -> list[str]:
    """Parse attributions.yml `files` field (list or legacy comma/newline string)."""
    if raw_files is None:
        return []
    if isinstance(raw_files, str):
        items = [raw_files]
    elif isinstance(raw_files, list):
        items = [str(x) for x in raw_files if x is not None]
    else:
        return []

    parsed: list[str] = []
    for item in items:
        for line in item.replace("\r", "\n").split("\n"):
            for part in line.split(","):
                name = part.strip().strip('"').strip("'")
                if name:
                    parsed.append(name.replace("\\", "/"))
    return parsed


def _find_nearest_attributions(
    start_dir: Path, stop_root: Path
) -> tuple[Path, list[dict[str, Any]]] | None:
    """Walk upward from start_dir up to stop_root looking for attributions.yml."""
    current = start_dir.resolve()
    stop_resolved = stop_root.resolve()
    while True:
        candidate = current / "attributions.yml"
        if candidate.is_file():
            try:
                text = candidate.read_text(encoding="utf-8-sig", errors="replace")
                data = load_yaml_document(text)
                if isinstance(data, list):
                    return candidate, [x for x in data if isinstance(x, dict)]
                return candidate, []
            except Exception:
                return candidate, []
        if current == stop_resolved or current.parent == current:
            break
        try:
            current.relative_to(stop_resolved)
        except ValueError:
            break
        current = current.parent
    return None


def _match_attribution(
    target_path: Path,
    attr_file: Path,
    entries: list[dict[str, Any]],
    *,
    extra_names: list[str] | None = None,
) -> dict[str, Any] | None:
    attr_dir = attr_file.parent.resolve()
    try:
        rel_from_attr = target_path.resolve().relative_to(attr_dir).as_posix()
    except ValueError:
        rel_from_attr = target_path.name

    candidates = {rel_from_attr.lower(), target_path.name.lower()}
    if extra_names:
        for n in extra_names:
            candidates.add(n.lower())

    for entry in entries:
        files = _parse_attribution_files(entry.get("files"))
        lower_files = {f.lstrip("./").lower() for f in files}
        if candidates & lower_files:
            return {
                "license": entry.get("license"),
                "copyright": entry.get("copyright"),
                "source": entry.get("source"),
            }
    return None


def _scan_audio_files(target_dir: Path) -> list[Path]:
    found: list[Path] = []
    stack: list[Path] = [target_dir]
    while stack:
        cur = stack.pop()
        try:
            with os.scandir(cur) as it:
                for entry in it:
                    if entry.is_dir(follow_symlinks=False):
                        stack.append(Path(entry.path))
                    elif entry.is_file(follow_symlinks=False):
                        if Path(entry.name).suffix.lower() in _AUDIO_EXTS:
                            found.append(Path(entry.path))
        except OSError:
            continue
    found.sort()
    return found


def _audit_audio_dir(
    repo_root: Path,
    target_dir: Path,
    check_attributions: bool,
) -> dict[str, Any]:
    """Audit all .ogg/.wav files in a directory against nearest attributions.yml."""
    audio_root = repo_root / "Resources" / "Audio"
    audio_files = _scan_audio_files(target_dir)
    attr_cache: dict[Path, tuple[Path, list[dict[str, Any]]] | None] = {}

    attributed_count = 0
    missing: list[str] = []
    attr_ymls: set[str] = set()
    issues: list[str] = []

    for af in audio_files:
        rel = to_posix_rel(af, repo_root)
        if not check_attributions:
            continue
        parent = af.parent
        if parent not in attr_cache:
            attr_cache[parent] = _find_nearest_attributions(parent, audio_root)
        found_attr = attr_cache[parent]
        if found_attr is None:
            missing.append(rel)
            issues.append(f"No attributions.yml found for audio '{rel}'.")
            continue
        attr_path, entries = found_attr
        attr_ymls.add(to_posix_rel(attr_path, repo_root))
        if _match_attribution(af, attr_path, entries) is not None:
            attributed_count += 1
        else:
            missing.append(rel)
            issues.append(
                f"Audio '{rel}' missing entry in {to_posix_rel(attr_path, repo_root)}."
            )

    return {
        "dir": to_posix_rel(target_dir, repo_root),
        "audio_files_count": len(audio_files),
        "attributed_count": attributed_count,
        "missing_attributions_count": len(missing),
        "attributions_yml": sorted(attr_ymls),
        "missing_attributions": missing[:50],
        "ok": len(issues) == 0,
        "issues": issues[:50],
    }


def audit_rsi(
    repo_root: Path,
    rsi_path: str = "",
    check_attributions: bool = True,
    audio_paths: list[str] | None = None,
    audio_dir: str | None = None,
    verbose: bool = False,
) -> dict[str, Any]:
    """Audit an .rsi directory and/or an audio directory (compact by default; verbose=True lists all states)."""
    # Standalone audio_dir audit if rsi_path is omitted
    if not rsi_path.strip() and audio_dir:
        resolved_adir = _resolve_asset_path(repo_root, audio_dir, "Resources/Audio")
        if resolved_adir is None or not resolved_adir.is_dir():
            return {
                "error": f"Audio directory not found for '{audio_dir}'.",
                "audio_dir": audio_dir,
            }
        return _audit_audio_dir(repo_root, resolved_adir, check_attributions)

    resolved_rsi = _resolve_asset_path(repo_root, rsi_path, "Resources/Textures")
    if resolved_rsi is None or not resolved_rsi.is_dir():
        return {
            "error": f"RSI or audio directory not found for '{rsi_path}'.",
            "rsi_path": rsi_path,
        }

    meta_file = resolved_rsi / "meta.json"
    if not meta_file.is_file():
        # Auto-detect audio directory if no meta.json is present
        audio_in_dir = _scan_audio_files(resolved_rsi)
        if audio_in_dir or "resources/audio/" in to_posix_rel(resolved_rsi, repo_root).lower():
            return _audit_audio_dir(repo_root, resolved_rsi, check_attributions)
        return {
            "error": f"meta.json not found inside '{to_posix_rel(resolved_rsi, repo_root)}'.",
            "rsi_path": to_posix_rel(resolved_rsi, repo_root),
        }

    try:
        meta = json.loads(meta_file.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        return {
            "error": f"Failed to parse meta.json: {exc}",
            "rsi_path": to_posix_rel(resolved_rsi, repo_root),
        }

    if not isinstance(meta, dict):
        return {
            "error": "meta.json root must be a JSON object.",
            "rsi_path": to_posix_rel(resolved_rsi, repo_root),
        }

    def _safe_int(val: Any, default: int = 0) -> int | None:
        if val is None or isinstance(val, bool):
            return default
        try:
            return int(val)
        except (TypeError, ValueError):
            return None

    issues: list[str] = []
    size_obj = meta.get("size")
    if not isinstance(size_obj, dict):
        issues.append(f"Invalid RSI size: {size_obj!r}")
        size_x = 0
        size_y = 0
    else:
        parsed_x = _safe_int(size_obj.get("x", 0), 0)
        parsed_y = _safe_int(size_obj.get("y", 0), 0)
        size_x = parsed_x if parsed_x is not None else 0
        size_y = parsed_y if parsed_y is not None else 0
        if parsed_x is None or parsed_y is None or size_x <= 0 or size_y <= 0:
            issues.append(f"Invalid RSI size: {size_obj!r}")

    states_raw = meta.get("states") or []
    if not isinstance(states_raw, list):
        issues.append("meta.json 'states' is not a list.")
        states_raw = []

    states_out: list[dict[str, Any]] = []
    png_hash_map: dict[str, list[str]] = {}
    referenced_pngs: set[str] = set()

    for state in states_raw:
        if not isinstance(state, dict) or not state.get("name"):
            issues.append(f"Malformed state entry: {state!r}")
            continue
        name = str(state["name"])
        parsed_dirs = _safe_int(state.get("directions", 1), 1)
        if parsed_dirs is None or parsed_dirs <= 0:
            issues.append(f"State '{name}': invalid directions {state.get('directions')!r}.")
            directions = 1
        else:
            directions = parsed_dirs
        delays = state.get("delays")

        expected_slots = directions
        frames_per_dir = 1
        if isinstance(delays, list):
            if len(delays) != directions:
                issues.append(
                    f"State '{name}': delays length ({len(delays)}) != directions ({directions})."
                )
            if delays and isinstance(delays[0], list):
                frames_per_dir = len(delays[0])
                expected_slots = sum(
                    len(d) for d in delays if isinstance(d, list)
                )

        png_name = f"{name}.png"
        referenced_pngs.add(png_name.lower())
        png_path = resolved_rsi / png_name

        state_info: dict[str, Any] = {
            "name": name,
            "directions": directions,
            "frames": frames_per_dir,
            "expected_slots": expected_slots,
            "exists": png_path.is_file(),
        }

        if not png_path.is_file():
            issues.append(f"State '{name}': missing PNG '{png_name}'.")
            states_out.append(state_info)
            continue

        raw_bytes = png_path.read_bytes()
        file_sha = _sha256_bytes(raw_bytes)
        state_info["sha256"] = file_sha
        png_hash_map.setdefault(file_sha, []).append(name)

        try:
            with Image.open(png_path) as img:
                width, height = img.size
                state_info["width"] = width
                state_info["height"] = height
                if size_x > 0 and size_y > 0:
                    if width % size_x != 0 or height % size_y != 0:
                        issues.append(
                            f"State '{name}': PNG dimensions {width}x{height} not divisible by {size_x}x{size_y}."
                        )
                    else:
                        cols = width // size_x
                        rows = height // size_y
                        grid_slots = cols * rows
                        state_info["grid_slots"] = grid_slots
                        if grid_slots < expected_slots:
                            issues.append(
                                f"State '{name}': grid has {grid_slots} slots but needs {expected_slots}."
                            )
                        if expected_slots > 1 and grid_slots >= expected_slots:
                            rgba = img.convert("RGBA")
                            frame_hashes: list[str] = []
                            for idx in range(expected_slots):
                                col = idx % cols
                                row = idx // cols
                                box = (
                                    col * size_x,
                                    row * size_y,
                                    (col + 1) * size_x,
                                    (row + 1) * size_y,
                                )
                                frame_hashes.append(
                                    _sha256_bytes(rgba.crop(box).tobytes())
                                )
                            unique_frames = len(set(frame_hashes))
                            state_info["unique_frames"] = unique_frames
                            if unique_frames == 1:
                                issues.append(
                                    f"State '{name}': all {expected_slots} frames are pixel-identical (placeholder)."
                                )
        except Exception as exc:
            issues.append(f"State '{name}': failed to read PNG: {exc}")

        states_out.append(state_info)

    duplicate_png_groups = [
        {"sha256": sha, "states": names}
        for sha, names in png_hash_map.items()
        if len(names) > 1
    ]
    if duplicate_png_groups:
        for dup in duplicate_png_groups:
            issues.append(
                f"Duplicate PNG bytes shared by states: {dup['states']} (sha256={dup['sha256'][:12]}...)"
            )

    disk_pngs: list[str] = []
    orphan_pngs: list[str] = []
    try:
        with os.scandir(resolved_rsi) as it:
            for entry in it:
                if entry.is_file(follow_symlinks=False) and entry.name.lower().endswith(".png"):
                    disk_pngs.append(entry.name)
                    if entry.name.lower() not in referenced_pngs:
                        orphan_pngs.append(entry.name)
    except OSError:
        pass
    orphan_pngs.sort()

    audio_results: list[dict[str, Any]] = []
    if audio_paths:
        audio_root = repo_root / "Resources" / "Audio"
        for ap in audio_paths:
            resolved_audio = _resolve_asset_path(repo_root, ap, "Resources/Audio")
            if resolved_audio is None or not resolved_audio.is_file():
                issues.append(f"Audio file not found: '{ap}'.")
                audio_results.append({"path": ap, "exists": False})
                continue
            item: dict[str, Any] = {
                "path": to_posix_rel(resolved_audio, repo_root),
                "exists": True,
                "size_bytes": resolved_audio.stat().st_size,
            }
            if check_attributions:
                found_attr = _find_nearest_attributions(
                    resolved_audio.parent, audio_root
                )
                if found_attr:
                    attr_path, entries = found_attr
                    matched = _match_attribution(resolved_audio, attr_path, entries)
                    item["attributions_yml"] = to_posix_rel(attr_path, repo_root)
                    item["attributed"] = matched is not None
                    if matched is None:
                        issues.append(
                            f"Audio '{item['path']}' missing entry in {item['attributions_yml']}."
                        )
                else:
                    item["attributed"] = False
                    issues.append(
                        f"No attributions.yml found for audio '{item['path']}'."
                    )
            audio_results.append(item)

    audio_dir_audit: dict[str, Any] | None = None
    if audio_dir:
        resolved_adir = _resolve_asset_path(repo_root, audio_dir, "Resources/Audio")
        if resolved_adir is None or not resolved_adir.is_dir():
            issues.append(f"Audio directory not found: '{audio_dir}'.")
            audio_dir_audit = {"error": f"Audio directory not found: '{audio_dir}'."}
        else:
            audio_dir_audit = _audit_audio_dir(repo_root, resolved_adir, check_attributions)
            if not audio_dir_audit["ok"]:
                issues.extend(audio_dir_audit["issues"])

    attributions_info: dict[str, Any] | None = None
    if check_attributions:
        meta_license = meta.get("license")
        meta_copyright = meta.get("copyright")
        textures_root = repo_root / "Resources" / "Textures"
        found_tex_attr = _find_nearest_attributions(resolved_rsi, textures_root)
        attr_yml_rel: str | None = None
        rsi_entry_matched = False
        if found_tex_attr:
            attr_path, entries = found_tex_attr
            attr_yml_rel = to_posix_rel(attr_path, repo_root)
            if _match_attribution(resolved_rsi, attr_path, entries) is not None:
                rsi_entry_matched = True
            else:
                existing_pngs = [
                    resolved_rsi / f"{st['name']}.png"
                    for st in states_out
                    if st.get("exists")
                ]
                if existing_pngs:
                    rsi_entry_matched = all(
                        _match_attribution(st_png, attr_path, entries) is not None
                        for st_png in existing_pngs
                    )

        attributions_info = {
            "meta_license": meta_license,
            "meta_copyright": meta_copyright,
            "has_meta_attribution": bool(meta_license and meta_copyright),
            "nearest_attributions_yml": attr_yml_rel,
            "matched_in_attributions_yml": rsi_entry_matched,
        }
        if not (meta_license and meta_copyright) and not rsi_entry_matched:
            issues.append(
                "Missing attribution: neither meta.json (license+copyright) nor attributions.yml covers this RSI."
            )

    result: dict[str, Any] = {
        "rsi_path": to_posix_rel(resolved_rsi, repo_root),
        "version": meta.get("version"),
        "size": {"x": size_x, "y": size_y},
        "states_in_meta": len(states_out),
        "png_on_disk": len(disk_pngs),
        "duplicate_png_groups": duplicate_png_groups,
        "orphan_pngs": orphan_pngs,
        "attributions": attributions_info,
        "ok": len(issues) == 0,
        "issues": issues,
    }
    if audio_results:
        result["audio"] = audio_results
    if audio_dir_audit is not None:
        result["audio_dir_audit"] = audio_dir_audit
    if verbose:
        result["states"] = states_out
    return result
