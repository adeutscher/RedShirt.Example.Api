#!/usr/bin/env python3
"""Delete an upload via the API, optionally hard-purging all records and storage."""

from __future__ import annotations

from upload_script_common import (
    api_request,
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)


def main() -> int:
    parser = create_parser(
        "Delete an upload (lifecycle tombstone) or hard-purge it and all related records."
    )
    parser.add_argument("upload_id", help="Upload id (GUID) to delete")
    parser.add_argument(
        "--purge",
        action="store_true",
        help=(
            "Hard-delete the upload from storage and remove all database records "
            "(DELETE /uploads/{id}?purge=true; requires upload:purge — use an admin or developer token)"
        ),
    )
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    path = f"/uploads/{args.upload_id}"
    if args.purge:
        path += "?purge=true"
    result = api_request(base_url, token, "DELETE", path)

    if args.purge:
        print(f"{args.upload_id}: purged")
    else:
        state = result.get("state", "")
        print(f"{args.upload_id}: deleted (state={state!r})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
