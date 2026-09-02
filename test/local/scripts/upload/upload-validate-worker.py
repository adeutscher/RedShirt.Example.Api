#!/usr/bin/env python3
"""Mock validator worker: validate a specific NotValidated upload and submit a verdict."""

from __future__ import annotations

import os
import urllib.request
from urllib.parse import urlparse, urlunparse

from upload_script_common import (
    api_request,
    create_parser,
    get_api_base_url,
    require_api_jwt_token,
)

EXPECTED_STATE = "NotValidated"

# Local docker-compose sets AWS_SERVICE_URL=http://ministack:4566 inside the API
# container, so presigned S3 download URLs use the Docker-only hostname "ministack".
# This script is meant to run on the developer machine (outside Docker), where
# ministack is reached via the published port on localhost (4566). Rewriting here
# is a deliberate local-testing carve-out; production workers call real S3 endpoints.
DEFAULT_LOCAL_MINISTACK_URL = "http://localhost:4566"
DOCKER_MINISTACK_HOSTNAME = "ministack"


def localize_ministack_download_url(download_url: str) -> str:
    """Map Docker-internal ministack hosts in presigned URLs to a host-reachable endpoint."""
    parsed = urlparse(download_url)
    if parsed.hostname != DOCKER_MINISTACK_HOSTNAME:
        return download_url

    local_endpoint = urlparse(
        os.environ.get("AWS_SERVICE_URL", DEFAULT_LOCAL_MINISTACK_URL)
    )
    scheme = local_endpoint.scheme or parsed.scheme or "http"
    hostname = local_endpoint.hostname or "localhost"
    port = local_endpoint.port or parsed.port or 4566
    netloc = f"{hostname}:{port}" if port else hostname
    return urlunparse(
        (scheme, netloc, parsed.path, parsed.params, parsed.query, parsed.fragment)
    )


def download_text(download_url: str) -> str:
    with urllib.request.urlopen(download_url, timeout=60) as response:
        return response.read().decode("utf-8", errors="replace")


def is_valid_potato_document(content: str) -> bool:
    return "potato" in content.casefold()


def main() -> int:
    parser = create_parser(
        "Mock validator worker: validate a specific NotValidated upload and submit a verdict. A valid document is a text document containing the word 'potato'."
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
    download_url = localize_ministack_download_url(link["downloadUrl"])
    content = download_text(download_url)
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
