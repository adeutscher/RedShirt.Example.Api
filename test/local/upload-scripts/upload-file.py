#!/usr/bin/env python3
"""Upload a local file via POST /uploads."""

from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
import urllib.error
import urllib.request

DEFAULT_API_BASE = "http://localhost:9000"


def main() -> int:
    parser = argparse.ArgumentParser(description="Upload a local file via POST /uploads")
    parser.add_argument("file", help="Path to the file to upload")
    parser.add_argument(
        "--file-name",
        help="X-File-Name header value (default: basename of the file path)",
    )
    parser.add_argument(
        "--idempotency-key",
        help="Idempotency-Key header value (default: random UUID)",
    )
    args = parser.parse_args()

    token = os.environ.get("API_JWT_TOKEN")
    if not token:
        raise SystemExit("Set API_JWT_TOKEN to a bearer access token.")

    base_url = os.environ.get("API_BASE_URL", DEFAULT_API_BASE)
    file_path = args.file
    if not os.path.isfile(file_path):
        raise SystemExit(f"File not found: {file_path}")

    with open(file_path, "rb") as file:
        content = file.read()

    file_name = args.file_name or os.path.basename(file_path)
    idempotency_key = args.idempotency_key or str(uuid.uuid4())

    url = f"{base_url.rstrip('/')}/uploads"
    request = urllib.request.Request(
        url,
        data=content,
        method="POST",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/json",
            "X-File-Name": file_name,
            "Idempotency-Key": idempotency_key,
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            summary = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        print(f"HTTP {error.code}: {body}", file=sys.stderr)
        return 1

    print(json.dumps(summary, indent=2))
    print(f"id={summary.get('id')} state={summary.get('state')} idempotency_key={idempotency_key}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
