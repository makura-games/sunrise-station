#!/usr/bin/env python3

import os
import re
import sys
from typing import NoReturn
from urllib.parse import urlparse

TRUSTED_BRANCHES = {"master", "stable"}
FULL_SHA_PATTERN = re.compile(r"^[0-9a-fA-F]{40}$")
INVALID_CDN_URL_MESSAGE = (
    "ROBUST_CDN_URL must be an HTTPS URL with a host and "
    "without user credentials, query, or fragment."
)


def fail(message: str) -> NoReturn:
    print(f"::error::{message}", file=sys.stderr)
    raise SystemExit(1)


def is_trusted_ref(value: str) -> bool:
    return bool(FULL_SHA_PATTERN.fullmatch(value)) or value in TRUSTED_BRANCHES


def validate_cdn_url(value: str) -> str:
    value = value.strip()
    try:
        parsed_url = urlparse(value)
        hostname = parsed_url.hostname
        _ = parsed_url.port  # Проверяем корректность порта.
    except ValueError as error:
        raise ValueError(INVALID_CDN_URL_MESSAGE) from error

    if (
        parsed_url.scheme != "https"
        or not hostname
        or parsed_url.username is not None
        or parsed_url.password is not None
        or parsed_url.query
        or parsed_url.fragment
        or any(character.isspace() for character in value)
    ):
        raise ValueError(INVALID_CDN_URL_MESSAGE)

    return value.rstrip("/") + "/"


def main() -> None:
    cdn_url = os.environ.get("ROBUST_CDN_URL", "").strip()
    if not cdn_url:
        fail("Repository variable ROBUST_CDN_URL is not configured.")

    try:
        validate_cdn_url(cdn_url)
    except ValueError as error:
        fail(str(error))

    if not os.environ.get("PUBLISH_FORK_ID", "").strip():
        fail("PUBLISH_FORK_ID is not configured or contains only whitespace.")

    commit_hash = os.environ.get("COMMIT_HASH", "")
    private_commit_hash = os.environ.get("PRIVATE_COMMIT_HASH", "")
    current_ref_name = os.environ.get("CURRENT_REF_NAME", "")
    current_ref_type = os.environ.get("CURRENT_REF_TYPE", "")

    if not commit_hash:
        if current_ref_type != "branch" or current_ref_name not in TRUSTED_BRANCHES:
            fail("The current ref must be the master or stable branch when commit_hash is empty.")
    elif not FULL_SHA_PATTERN.fullmatch(commit_hash):
        fail("commit_hash must be a full 40-character SHA.")

    if private_commit_hash and not is_trusted_ref(private_commit_hash):
        fail("private_commit_hash must be a full 40-character SHA or the master/stable branch.")

    use_private_content = os.environ.get("USE_PRIVATE_CONTENT", "").casefold() == "true"
    if use_private_content:
        if not os.environ.get("PRIVATE_REPOSITORY", "").strip():
            fail("PRIVATE_REPOSITORY is required when private content is enabled.")

        if not os.environ.get("PRIVATE_REPOSITORY_SSH_KEY", "").strip():
            fail("PRIVATE_REPOSITORY_SSH_KEY is required when private content is enabled.")


if __name__ == "__main__":
    main()
