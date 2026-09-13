#!/usr/bin/env python3

import argparse
import requests
import os
import subprocess
from validate_publish import validate_cdn_url
from typing import Iterable

PUBLISH_TOKEN = os.environ["PUBLISH_TOKEN"]
VERSION = subprocess.check_output(["git", "rev-parse", "HEAD"], encoding="UTF-8").strip()

RELEASE_DIR = "release"
HTTP_TIMEOUT = (15, 60)
UPLOAD_TIMEOUT = (15, 600)

ROBUST_CDN_URL = validate_cdn_url(os.environ["ROBUST_CDN_URL"])

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--fork-id", required=True)

    args = parser.parse_args()
    if not args.fork_id.strip():
        parser.error("--fork-id must not be empty or contain only whitespace")

    fork_id = args.fork_id.strip()

    session = requests.Session()
    session.headers = {
        "Authorization": f"Bearer {PUBLISH_TOKEN}",
    }

    print(f"Starting publish on Robust.Cdn for version {VERSION}")

    data = {
        "version": VERSION,
        "engineVersion": get_engine_version(),
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/start", json=data, headers=headers, timeout=HTTP_TIMEOUT)
    resp.raise_for_status()
    print("Publish successfully started, adding files...")

    for file in get_files_to_publish():
        print(f"Publishing {file}")
        with open(file, "rb") as f:
            headers = {
                "Content-Type": "application/octet-stream",
                "Robust-Cdn-Publish-File": os.path.basename(file),
                "Robust-Cdn-Publish-Version": VERSION
            }
            resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/file", data=f, headers=headers, timeout=UPLOAD_TIMEOUT)

        resp.raise_for_status()

    print("Successfully pushed files, finishing publish...")

    data = {
        "version": VERSION
    }
    headers = {
        "Content-Type": "application/json"
    }
    resp = session.post(f"{ROBUST_CDN_URL}fork/{fork_id}/publish/finish", json=data, headers=headers, timeout=HTTP_TIMEOUT)
    resp.raise_for_status()

    print("SUCCESS!")


def get_files_to_publish() -> Iterable[str]:
    for file in os.listdir(RELEASE_DIR):
        yield os.path.join(RELEASE_DIR, file)


def get_engine_version() -> str:
    proc = subprocess.run(["git", "describe","--tags", "--abbrev=0"], stdout=subprocess.PIPE, cwd="RobustToolbox", check=True, encoding="UTF-8")
    tag = proc.stdout.strip()
    assert tag.startswith("v")
    return tag[1:] # Cut off v prefix.


if __name__ == '__main__':
    main()
