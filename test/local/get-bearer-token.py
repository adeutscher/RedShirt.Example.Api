#!/usr/bin/env python3
"""Fetch a JWT access token from the local Keycloak realm (stdlib only).

Examples:
  ./get-bearer-token.py
  ./get-bearer-token.py --print-header
  ./get-bearer-token.py --grant client_credentials
  ./get-bearer-token.py --username testuser --password testpass
"""

from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_TOKEN_URL = (
    "http://localhost:9080/realms/example/protocol/openid-connect/token"
)
DEFAULT_CLIENT_ID = "example-api"
DEFAULT_SERVICE_CLIENT_ID = "example-service"
DEFAULT_SERVICE_CLIENT_SECRET = "example-service-secret"
DEFAULT_USERNAME = "testuser"
DEFAULT_PASSWORD = "testpass"


def request_token(token_url: str, form: dict[str, str]) -> dict:
    body = urllib.parse.urlencode(form).encode("utf-8")
    request = urllib.request.Request(
        token_url,
        data=body,
        method="POST",
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise SystemExit(f"Token request failed ({exc.code}): {detail}") from exc
    except urllib.error.URLError as exc:
        raise SystemExit(
            f"Unable to reach Keycloak at {token_url}: {exc.reason}"
        ) from exc

    if "access_token" not in payload:
        raise SystemExit(f"Unexpected token response: {payload}")
    return payload


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Obtain a bearer access token from local Keycloak."
    )
    parser.add_argument(
        "--token-url",
        default=DEFAULT_TOKEN_URL,
        help=f"OpenID token endpoint (default: {DEFAULT_TOKEN_URL})",
    )
    parser.add_argument(
        "--grant",
        choices=("password", "client_credentials"),
        default="password",
        help="OAuth grant type (default: password)",
    )
    parser.add_argument(
        "--client-id",
        default=None,
        help="OAuth client id (defaults depend on --grant)",
    )
    parser.add_argument(
        "--client-secret",
        default=DEFAULT_SERVICE_CLIENT_SECRET,
        help="Client secret for client_credentials grant",
    )
    parser.add_argument("--username", default=DEFAULT_USERNAME)
    parser.add_argument("--password", default=DEFAULT_PASSWORD)
    parser.add_argument(
        "--print-header",
        action="store_true",
        help="Print a full Authorization header instead of the raw token",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Print the full token endpoint JSON response",
    )
    args = parser.parse_args()

    if args.grant == "password":
        client_id = args.client_id or DEFAULT_CLIENT_ID
        form = {
            "grant_type": "password",
            "client_id": client_id,
            "username": args.username,
            "password": args.password,
        }
    else:
        client_id = args.client_id or DEFAULT_SERVICE_CLIENT_ID
        form = {
            "grant_type": "client_credentials",
            "client_id": client_id,
            "client_secret": args.client_secret,
        }

    payload = request_token(args.token_url, form)
    if args.json:
        json.dump(payload, sys.stdout, indent=2)
        sys.stdout.write("\n")
        return 0

    token = payload["access_token"]
    if args.print_header:
        sys.stdout.write(f"Authorization: Bearer {token}\n")
    else:
        sys.stdout.write(f"{token}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
