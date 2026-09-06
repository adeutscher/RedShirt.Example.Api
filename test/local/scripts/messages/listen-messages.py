#!/usr/bin/env python3
"""Listen to GET /messages/event-stream and print each example-message event."""

from __future__ import annotations

import sys
import urllib.error
import urllib.request

from messages_script_common import (
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)


def parse_sse_stream(response: urllib.response.addinfourl) -> None:
    event_name = None
    data_lines: list[str] = []

    while True:
        line = response.readline()
        if not line:
            break

        text = line.decode("utf-8", errors="replace").rstrip("\r\n")
        if not text:
            if data_lines:
                payload = "\n".join(data_lines)
                label = event_name or "message"
                print(f"[{label}] {payload}")
                sys.stdout.flush()
            event_name = None
            data_lines = []
            continue

        if text.startswith(":"):
            continue

        if text.startswith("event:"):
            event_name = text[len("event:") :].strip()
            continue

        if text.startswith("data:"):
            data_lines.append(text[len("data:") :].strip())
            continue


def main() -> int:
    parser = create_parser(
        "Listen to GET /messages/event-stream and print each example-message event"
    )
    args = parser.parse_args()
    _ = args

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    url = f"{base_url.rstrip('/')}/messages/event-stream"

    request = urllib.request.Request(
        url,
        method="GET",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "text/event-stream",
        },
    )

    print(f"Listening on {url} (Ctrl+C to stop)...", flush=True)

    try:
        with urllib.request.urlopen(request, timeout=None) as response:
            parse_sse_stream(response)
    except KeyboardInterrupt:
        print("\nStopped.", flush=True)
        return 0
    except urllib.error.HTTPError as error:
        body_text = error.read().decode("utf-8", errors="replace")
        print(f"HTTP {error.code}: {body_text}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
