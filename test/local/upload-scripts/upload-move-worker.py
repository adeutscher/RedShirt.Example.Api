#!/usr/bin/env python3
"""Mock mover worker: poll Verified uploads, copy S3 object, submit move report."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.request

DEFAULT_API_BASE = "http://localhost:9000"
POLL_STATE = "Verified"
DEFAULT_UNVERIFIED_BUCKET = "unverified-uploads"
DEFAULT_VERIFIED_BUCKET = "verified-uploads"


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
        return json.loads(response.read().decode("utf-8"))


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
    token = os.environ.get("API_JWT_TOKEN")
    if not token:
        raise SystemExit("Set API_JWT_TOKEN to a bearer access token.")

    base_url = os.environ.get("API_BASE_URL", DEFAULT_API_BASE)
    unverified = os.environ.get("UPLOADS__BUCKET_UNVERIFIED_ITEMS", DEFAULT_UNVERIFIED_BUCKET)
    verified = os.environ.get("UPLOADS__BUCKET_VERIFIED_ITEMS", DEFAULT_VERIFIED_BUCKET)

    search = api_request(base_url, token, "GET", f"/uploads?state={POLL_STATE}&pageSize=20")
    records = search.get("records", [])
    if not records:
        print("No Verified uploads found.")
        return 0

    for record in records:
        upload_id = record["id"]
        details = api_request(base_url, token, "GET", f"/uploads/{upload_id}/details")
        object_key = storage_object_key(details)
        s3_copy(unverified, verified, object_key)
        api_request(
            base_url,
            token,
            "POST",
            f"/uploads/{upload_id}/move-reports",
            {"verifiedStorageObjectKey": object_key},
        )
        print(f"{upload_id}: moved to {verified}/{object_key}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
