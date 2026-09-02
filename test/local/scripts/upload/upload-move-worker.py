#!/usr/bin/env python3
"""Mock mover worker: copy a specific Verified upload in S3 and submit a move report."""

from __future__ import annotations

import os
import subprocess

from upload_script_common import (
    api_request,
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)

EXPECTED_STATE = "Verified"
DEFAULT_UNVERIFIED_BUCKET = "unverified-uploads"
DEFAULT_VERIFIED_BUCKET = "verified-uploads"


def storage_object_key(details: dict) -> str:
    key = details.get("storageObjectKey")
    if not key:
        raise RuntimeError("storageObjectKey not found on upload details")
    return key


def s3_copy(source_bucket: str, dest_bucket: str, object_key: str) -> None:
    env = os.environ.copy()
    env.setdefault("AWS_DEFAULT_REGION", "us-east-1")
    env.setdefault("AWS_ACCESS_KEY_ID", "foo")
    env.setdefault("AWS_SECRET_ACCESS_KEY", "bar")
    endpoint = os.environ.get("AWS_SERVICE_URL", "http://localhost:4566")
    source = f"s3://{source_bucket}/{object_key}"
    dest = f"s3://{dest_bucket}/{object_key}"
    subprocess.run(
        ["awslocal", "s3", "cp", source, dest, "--endpoint-url", endpoint],
        check=True,
        env=env,
    )


def main() -> int:
    parser = create_parser(
        "Mock mover worker: copy a specific Verified upload in S3 and submit a move report."
    )
    parser.add_argument("upload_id", help="Upload id (GUID) to move")
    args = parser.parse_args()

    token = require_api_jwt_token()
    base_url = get_api_base_url()
    unverified = os.environ.get(
        "UPLOADS__BUCKET_UNVERIFIED_ITEMS", DEFAULT_UNVERIFIED_BUCKET
    )
    verified = os.environ.get("UPLOADS__BUCKET_VERIFIED_ITEMS", DEFAULT_VERIFIED_BUCKET)

    summary = api_request(base_url, token, "GET", f"/uploads/{args.upload_id}")
    state = summary.get("state")
    if state != EXPECTED_STATE:
        raise SystemExit(
            f"Upload {args.upload_id} is {state!r}; expected {EXPECTED_STATE!r}."
        )

    details = api_request(base_url, token, "GET", f"/uploads/{args.upload_id}/details")
    object_key = storage_object_key(details)
    s3_copy(unverified, verified, object_key)
    api_request(
        base_url,
        token,
        "POST",
        f"/uploads/{args.upload_id}/move-reports",
        {"verifiedStorageObjectKey": object_key},
    )
    print(f"{args.upload_id}: moved to {verified}/{object_key}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
