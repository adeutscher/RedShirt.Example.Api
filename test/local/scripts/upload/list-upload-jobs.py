#!/usr/bin/env python3
"""List uploads that are still being processed (non-terminal states)."""

from __future__ import annotations

from urllib.parse import urlencode

from upload_script_common import (
    api_request,
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)

PROCESSING_STATES = frozenset({"Uploading", "NotValidated", "Verified"})


def build_search_path(page_size: int, continuation_token: str | None = None) -> str:
    params: dict[str, str | int] = {"pageSize": page_size}
    if continuation_token:
        params["continuationToken"] = continuation_token
    return f"/uploads?{urlencode(params)}"


def fetch_upload_records(base_url: str, token: str, page_size: int) -> list[dict]:
    records: list[dict] = []
    path = build_search_path(page_size)
    while path:
        search = api_request(base_url, token, "GET", path)
        records.extend(search.get("records", []))
        continuation_token = search.get("continuationToken")
        path = (
            build_search_path(page_size, continuation_token)
            if continuation_token
            else None
        )
    return records


def main() -> int:
    parser = create_parser(
        "List uploads being processed (Uploading, NotValidated, Verified)."
    )
    parser.add_argument(
        "-a",
        "--all",
        action="store_true",
        help="List uploads in any state (default: only in-flight processing states)",
    )
    parser.add_argument(
        "--page-size",
        type=int,
        default=100,
        help="Page size for GET /uploads while walking continuation tokens (default: 100)",
    )
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    records = fetch_upload_records(base_url, token, args.page_size)
    if not args.all:
        # Filter down to active records
        records = [
            record for record in records if record.get("state") in PROCESSING_STATES
        ]

    if not records:
        print(
            "No uploads found."
            if args.all
            else "No uploads are currently being processed."
        )
        return 0

    print(f"{'upload id':36}  {'state':14}  file name")
    print(f"{'-' * 36}  {'-' * 14}  {'-' * 9}")
    for record in records:
        upload_id = record.get("id", "")
        state = record.get("state", "")
        file_name = record.get("fileName", "")
        print(f"{upload_id:36}  {state:14}  {file_name}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
