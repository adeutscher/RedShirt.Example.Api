#!/usr/bin/env python3
"""Mock validator worker: validate a specific NotValidated upload and submit a verdict."""

from __future__ import annotations

import urllib.request

from upload_script_common import (
    api_request,
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)

EXPECTED_STATE = "NotValidated"


def download_text(download_url: str) -> str:
    with urllib.request.urlopen(download_url, timeout=60) as response:
        return response.read().decode("utf-8", errors="replace")


def is_valid_potato_document(content: str) -> bool:
    return "potato" in content.casefold()


def main() -> int:
    parser = create_parser(
        "Mock validator worker: validate a specific NotValidated upload and submit a verdict."
    )
    parser.add_argument("upload_id", help="Upload id (GUID) to validate")
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    summary = api_request(base_url, token, "GET", f"/uploads/{args.upload_id}")
    state = summary.get("state")
    if state != EXPECTED_STATE:
        raise SystemExit(
            f"Upload {args.upload_id} is {state!r}; expected {EXPECTED_STATE!r}."
        )

    link = api_request(
        base_url, token, "GET", f"/uploads/{args.upload_id}/download-link"
    )
    content = download_text(link["downloadUrl"])
    approved = is_valid_potato_document(content)
    api_request(
        base_url,
        token,
        "POST",
        f"/uploads/{args.upload_id}/verdicts",
        {"approved": approved},
    )
    print(f"{args.upload_id}: approved={approved}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
