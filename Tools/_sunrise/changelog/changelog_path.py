from pathlib import Path, PurePosixPath, PureWindowsPath


def validate_changelog_path(value: str | None) -> Path:
    if not value or not value.strip():
        raise RuntimeError("Переменная CHANGELOG_FILE не задана")

    raw_path = value.strip()
    posix_path = PurePosixPath(raw_path)
    windows_path = PureWindowsPath(raw_path)
    path = Path(raw_path)
    if (
        posix_path.is_absolute()
        or windows_path.is_absolute()
        or windows_path.drive
        or windows_path.root
        or ".." in posix_path.parts
        or ".." in windows_path.parts
    ):
        raise RuntimeError("CHANGELOG_FILE должен быть относительным путём внутри репозитория")
    if path.parent == Path("."):
        raise RuntimeError("CHANGELOG_FILE должен включать родительский каталог")
    return path
