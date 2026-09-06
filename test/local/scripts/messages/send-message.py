#!/usr/bin/env python3
"""Publish an example message event via POST /messages."""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request

from messages_script_common import (
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)


def main() -> int:
    parser = create_parser("Publish an example message event via POST /messages")
    parser.add_argument(
        "message",
        nargs="?",
        default="Hello from local test script",
        help="Message text to publish (default: Hello from local test script)",
    )
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    url = f"{base_url.rstrip('/')}/messages"
    body = json.dumps({"message": args.message}).encode("utf-8")

    request = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/json",
            "Content-Type": "application/json",
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            print(f"HTTP {response.status} {response.reason}")
    except urllib.error.HTTPError as error:
        body_text = error.read().decode("utf-8", errors="replace")
        print(f"HTTP {error.code}: {body_text}", file=sys.stderr)
        return 1

    print(f"Published message for the authenticated user: {args.message!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
