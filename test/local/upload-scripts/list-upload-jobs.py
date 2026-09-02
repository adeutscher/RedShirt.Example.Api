#!/usr/bin/env python3
"""List uploads that are still being processed (non-terminal states)."""

from __future__ import annotations

from upload_script_common import api_request, create_parser, get_api_base_url, require_api_jwt_token

PROCESSING_STATES = frozenset({"Uploading", "NotValidated", "Verified"})


def main() -> int:
    parser = create_parser(
        "List uploads being processed (Uploading, NotValidated, Verified)."
    )
    parser.add_argument(
        "--page-size",
        type=int,
        default=100,
        help="Maximum records to fetch from GET /uploads (default: 100)",
    )
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    search = api_request(base_url, token, "GET", f"/uploads?pageSize={args.page_size}")
    records = search.get("records", [])
    processing = [record for record in records if record.get("state") in PROCESSING_STATES]

    if not processing:
        print("No uploads are currently being processed.")
        return 0

    print(f"{'upload id':36}  {'state':14}  file name")
    print(f"{'-' * 36}  {'-' * 14}  {'-' * 9}")
    for record in processing:
        upload_id = record.get("id", "")
        state = record.get("state", "")
        file_name = record.get("fileName", "")
        print(f"{upload_id:36}  {state:14}  {file_name}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
