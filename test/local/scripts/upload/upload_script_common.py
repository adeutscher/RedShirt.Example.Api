"""Shared helpers for local upload test scripts."""

from __future__ import annotations

import argparse
import json
import os
import urllib.error
import urllib.request

DEFAULT_API_BASE = "http://localhost:9000"
API_JWT_TOKEN_ENV = "API_JWT_TOKEN"
API_BASE_URL_ENV = "API_BASE_URL"

ENV_HELP_EPILOG = f"""\
environment variables:
  {API_JWT_TOKEN_ENV}    Bearer access token (required). Populate from get-bearer-token.py, e.g.:
                         export {API_JWT_TOKEN_ENV}="$(./test/local/get-bearer-token.py)"
  {API_BASE_URL_ENV}    API base URL (default: {DEFAULT_API_BASE})
"""


def create_parser(description: str) -> argparse.ArgumentParser:
    return argparse.ArgumentParser(
        description=description,
        epilog=ENV_HELP_EPILOG,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )


def require_api_jwt_token() -> str:
    token = os.environ.get(API_JWT_TOKEN_ENV)
    if not token:
        raise SystemExit(
            f"Set {API_JWT_TOKEN_ENV} to a bearer access token "
            f'(see --help; e.g. export {API_JWT_TOKEN_ENV}="$(./test/local/get-bearer-token.py)").'
        )
    return token


def get_api_base_url() -> str:
    return os.environ.get(API_BASE_URL_ENV, DEFAULT_API_BASE)


def api_request(
    base_url: str,
    token: str,
    method: str,
    path: str,
    body: dict | None = None,
) -> dict:
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
    with urllib.request.urlopen(request, timeout=120) as response:
        if response.status == 204:
            return {}
        raw = response.read()
        if not raw:
            return {}
        return json.loads(raw.decode("utf-8"))
