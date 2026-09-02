#!/usr/bin/env python3
"""Mock validator worker: poll NotValidated uploads, validate, submit verdicts."""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request

from upload_script_common import create_parser, get_api_base_url, require_api_jwt_token

POLL_STATE = "NotValidated"


def api_request(base_url: str, token: str, method: str, path: str, body: dict | None = None) -> dict:
    url = f"{base_url.rstrip('/')}{path}"
    data = None if body is None else json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        method=method,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/json",
            **({} if body is None else {"Content-Type": "application/json"}),
        },
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        if response.status == 204 or not response.length:
            return {}
        return json.loads(response.read().decode("utf-8"))


def download_text(download_url: str) -> str:
    with urllib.request.urlopen(download_url, timeout=60) as response:
        return response.read().decode("utf-8", errors="replace")


def is_valid_potato_document(content: str) -> bool:
    return "potato" in content.casefold()


def main() -> int:
    create_parser(
        "Mock validator worker: poll NotValidated uploads, validate, submit verdicts."
    ).parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    search = api_request(
        base_url,
        token,
        "GET",
        f"/uploads?state={POLL_STATE}&pageSize=20",
    )
    records = search.get("records", [])
    if not records:
        print("No NotValidated uploads found.")
        return 0

    for record in records:
        upload_id = record["id"]
        link = api_request(base_url, token, "GET", f"/uploads/{upload_id}/download-link")
        content = download_text(link["downloadUrl"])
        approved = is_valid_potato_document(content)
        api_request(
            base_url,
            token,
            "POST",
            f"/uploads/{upload_id}/verdicts",
            {"approved": approved},
        )
        print(f"{upload_id}: approved={approved}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
