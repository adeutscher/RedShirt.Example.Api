#!/usr/bin/env python3
"""Mock cleanup worker: poll Rejected uploads and DELETE them via the API."""

from __future__ import annotations

import json
import os
import sys
import urllib.request

DEFAULT_API_BASE = "http://localhost:9000"
POLL_STATE = "Rejected"


def api_request(base_url: str, token: str, method: str, path: str) -> dict:
    url = f"{base_url.rstrip('/')}{path}"
    request = urllib.request.Request(
        url,
        method=method,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read().decode("utf-8"))


def main() -> int:
    token = os.environ.get("API_JWT_TOKEN")
    if not token:
        raise SystemExit("Set API_JWT_TOKEN to a bearer access token.")

    base_url = os.environ.get("API_BASE_URL", DEFAULT_API_BASE)
    search = api_request(base_url, token, "GET", f"/uploads?state={POLL_STATE}&pageSize=20")
    records = search.get("records", [])
    if not records:
        print("No Rejected uploads found.")
        return 0

    for record in records:
        upload_id = record["id"]
        api_request(base_url, token, "DELETE", f"/uploads/{upload_id}")
        print(f"{upload_id}: deleted")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
