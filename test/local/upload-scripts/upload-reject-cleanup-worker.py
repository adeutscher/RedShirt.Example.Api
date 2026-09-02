#!/usr/bin/env python3
"""Mock cleanup worker: DELETE a specific Rejected upload via the API."""

from __future__ import annotations

from upload_script_common import api_request, create_parser, get_api_base_url, require_api_jwt_token

EXPECTED_STATE = "Rejected"


def main() -> int:
    parser = create_parser(
        "Mock cleanup worker: DELETE a specific Rejected upload via the API."
    )
    parser.add_argument("upload_id", help="Upload id (GUID) to delete")
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    summary = api_request(base_url, token, "GET", f"/uploads/{args.upload_id}")
    state = summary.get("state")
    if state != EXPECTED_STATE:
        raise SystemExit(
            f"Upload {args.upload_id} is {state!r}; expected {EXPECTED_STATE!r}."
        )

    api_request(base_url, token, "DELETE", f"/uploads/{args.upload_id}")
    print(f"{args.upload_id}: deleted")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
