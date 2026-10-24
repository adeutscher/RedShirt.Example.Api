#!/usr/bin/env python3
"""Shape-driven integration test for the basic RESTful CRUD controllers of a running API container.

The script reads every OpenAPI v3 ``swagger.json`` under the repository's ``swagger/`` directory, groups
operations into controllers by OpenAPI tag, and decides what each controller is from its HTTP verbs and
request/response model shapes. It never keys behaviour off of path text or model names, so it keeps
working in an applied copy of the example template after controllers and models are renamed.

For each basic CRUD controller it creates and reads records (POST / PUT / GET-by-ID), then exercises
PATCH, search/list, and DELETE when the controller has them. Records created by the script are deleted
afterwards unless ``--keep`` is given.

Assumptions:
  * Supporting services were started with ``test/local/scripts/start-supporting-services.sh``.
  * The ``api`` Compose service is up (``docker compose up -d api``).
  * This script is not Docker-aware beyond reading the Compose file for published ports and for the
    Keycloak realm import file.

Examples:
  ./scripts/test/test-crud-controllers.py
  ./scripts/test/test-crud-controllers.py --keep
  ./scripts/test/test-crud-controllers.py --only Order --only Product -v
  ./scripts/test/test-crud-controllers.py --classify-only
  ./scripts/test/test-crud-controllers.py --config-dir /path/to/other/api/test/local

Exit codes:
  0  All tested controllers passed (also when a swagger document has no basic CRUD controllers).
  1  At least one controller test failed.
  2  Setup problem (no swagger documents, unreadable config or Compose file, no controllers at all).
  3  The API or Keycloak port is not open on localhost.
"""

from __future__ import annotations

import argparse
import datetime as dt
import email.utils
import json
import os
import random
import re
import socket
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterator

try:
    import yaml
except ImportError:  # pragma: no cover - environment guard
    sys.stderr.write("PyYAML is required: pip install pyyaml\n")
    raise SystemExit(2)

SCRIPT_DIR = Path(__file__).resolve().parent

ENV_CONFIG_FILE = "CRUD_TEST_CONFIG"
ENV_CONFIG_DIR = "CRUD_TEST_CONFIG_DIR"
ENV_COMPOSE_FILE = "CRUD_TEST_COMPOSE_FILE"
ENV_SWAGGER_DIR = "CRUD_TEST_SWAGGER_DIR"

# Default config directory is test/local/ (two levels above test/local/scripts/test/).
DEFAULT_CONFIG_DIR = SCRIPT_DIR.parent.parent
CONFIG_FILE_NAME = "crud-test.yaml"
COMPOSE_FILE_NAMES = (
    "docker-compose.yaml",
    "docker-compose.yml",
    "compose.yaml",
    "compose.yml",
)

DEFAULT_API_SERVICE = "api"
DEFAULT_KEYCLOAK_SERVICE = "keycloak"
DEFAULT_REALM = "test"
DEFAULT_USERNAME = "test-admin"
DEFAULT_PASSWORD = "test-admin-pass"

HTTP_TIMEOUT_SECONDS = 30
MAX_BACKOFF_SECONDS = 120
MAX_RETRY_ATTEMPTS = 8
MAX_LIST_PAGES = 500

# Dependency detection: a field or parameter links to another controller when its name, with one of
# these suffixes removed, equals that controller's tag (case-insensitive). The bare name also counts
# (no suffix removed). Add suffixes here to widen detection.
#   * "Id": Order.customerId -> Customer (example API), CharacterLocationDto.characterId -> Character
#     (Realm API), ItemStackInfoPutRequest.itemId -> Item (World API). Observed 2026-10-23.
#   * bare: ItemPutRequest.icon -> Icon (World API). Observed 2026-10-23.
DEPENDENCY_SUFFIXES = ("Id",)

# Fraction of write-model fields that must also appear in the read model for a controller to count as
# basic CRUD. Upload in the example API (2026-10-23) fails this because its create is not JSON at all.
WRITE_READ_OVERLAP_THRESHOLD = 0.5
# Fraction of item fields shared between a written collection and the read collection for the
# "collection save" special case (Realm API CharacterInventory / CharacterActionBar, 2026-10-23:
# CharacterInventoryWriteSlot vs CharacterInventorySlotModel).
COLLECTION_OVERLAP_THRESHOLD = 0.6
# Fraction of read-model fields an array item must share to count as a list of that read model.
LIST_ITEM_OVERLAP_THRESHOLD = 0.8
# Fraction of read-model fields a PATCH response must share with the read model.
PATCH_RESPONSE_OVERLAP_THRESHOLD = 0.6

# Field names that mark a base64-encoded file payload. World API Icon (2026-10-23) sends icon images
# as IconPostRequest.base64Data in JSON instead of multipart, so it is not caught by content type.
BASE64_FIELD_NAMES = ("base64data",)
BINARY_CONTENT_TYPES = ("multipart/", "application/octet-stream")
BINARY_FORMATS = ("binary", "byte")

HTTP_METHODS = ("get", "post", "put", "patch", "delete")

# Extended checks.
# A server timestamp further than this from the local clock fails the "current" check.
TIMESTAMP_SKEW_SECONDS = 600
# Pages read with a page size of 1 when checking that paging is clean.
PAGING_MAX_PAGES = 30
# Pages read for each exact-match filter query.
FILTER_MAX_PAGES = 10
# Problems listed per check before the rest are summarized as a count.
MAX_ISSUES_SHOWN = 5
# A request body that is not valid JSON, for the bad request body checks.
MALFORMED_JSON = b'{"crudTest": '
# Larger than any 64-bit integer type, for the integer overflow check.
INTEGER_OVERFLOW = 10**20
# Response header prefixes that announce rate limits (IETF RateLimit draft and the common X- forms).
RATE_LIMIT_HEADER_PREFIXES = ("ratelimit", "x-ratelimit", "x-rate-limit")
GUID_PATTERN = re.compile(
    r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
)

# Authorization header variants for the 401 checks.
AUTH_TOKEN = "token"
AUTH_NONE = "none"
AUTH_GARBLED = "garbled"
AUTH_TAMPERED = "tampered"

# Possible future checks, not implemented yet:
#   * Reader role cannot write: writes as a configured read-only user answer 403 and reads answer 200.
#   * Idempotency-Key replay: a POST repeated with the same Idempotency-Key returns the first record
#     instead of creating a second one.
#   * Parent existence is enforced: a parent-keyed write (Realm API CharacterLocation PUT /{id}) for an
#     unknown parent answers 404. This version of the standard does not enforce a parent's existence and
#     treats children as standalone tables, so today any non-5xx answer passes.
#   * Deleting a parent that still has children answers 409 instead of a 500 from a foreign key. The
#     current standard does not cover automatic cascades, so a cascading delete would need its own
#     expectation.
#   * Range and partial-match filters (Contains, GreaterThan, LessThan, IsNull suffixes). Only exact
#     filters named after read-model fields are checked today.
#   * Status codes are among those the OpenAPI document declares for the operation.
#   * Concurrent identical writes answer 2xx, 304, or 409 without 500s from lock contention.
#   * Uniqueness conflicts (409) for fields that config declares unique.
#   * Missing rate-limit headers and a 429 without Retry-After fail instead of being noted.

VERBOSE = False
WARNED: set[str] = set()

# Standard opt-out variable (https://no-color.org/): any non-empty value disables colour.
ENV_NO_COLOR = "NO_COLOR"


# ---------------------------------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------------------------------

# Palette and message headers follow core-tools (scripts/clipboard/python/messages.py).
PALETTE = {
    "purple": "\033[1;35m",
    "red": "\033[1;91m",
    "green": "\033[1;92m",
    "yellow": "\033[1;93m",
    "blue": "\033[1;94m",
    "bold": "\033[1m",
}
COLOUR_OFF = "\033[0m"
# Streams that get colour. Each is enabled only when it is a terminal.
COLOUR_STREAMS: set[int] = set()


def enable_colours(enabled: bool) -> None:
    COLOUR_STREAMS.clear()
    if not enabled:
        return
    for stream in (sys.stdout, sys.stderr):
        try:
            if stream.isatty():
                COLOUR_STREAMS.add(stream.fileno())
        except (AttributeError, OSError, ValueError):
            pass


def colour(text: str, name: str = "bold", stream=None) -> str:
    stream = stream or sys.stdout
    try:
        enabled = stream.fileno() in COLOUR_STREAMS
    except (AttributeError, OSError, ValueError):
        enabled = False
    return f"{PALETTE[name]}{text}{COLOUR_OFF}" if enabled else text


def fpath(value: Any, stream=None) -> str:
    """File paths are green (core-tools convention). Pass sys.stderr for warning/error text."""
    return colour(str(value), "green", stream)


def _print_message(header: str, header_colour: str, message: str) -> None:
    script = os.path.basename(sys.argv[0])
    sys.stderr.write(
        f"{colour(header, header_colour, sys.stderr)}"
        f"[{colour(script, 'green', sys.stderr)}]: {message}\n"
    )
    sys.stderr.flush()


def info(message: str) -> None:
    sys.stdout.write(f"{message}\n")
    sys.stdout.flush()


def debug(message: str) -> None:
    if VERBOSE:
        sys.stdout.write(f"    {colour('.', 'purple')} {message}\n")
        sys.stdout.flush()


def warn(message: str, once: bool = False) -> None:
    if once:
        if message in WARNED:
            return
        WARNED.add(message)
    _print_message("Warning", "yellow", message)


def error(message: str) -> None:
    _print_message("Error", "red", message)


class SetupError(Exception):
    """Configuration or environment problem that stops the run (exit code 2)."""


class PortClosedError(Exception):
    """A required port is not open on localhost (exit code 3)."""


def norm(name: str) -> str:
    return name.lower()


def truncate(text: str, limit: int = 300) -> str:
    text = text.strip().replace("\n", " ")
    return text if len(text) <= limit else text[: limit - 3] + "..."


# ---------------------------------------------------------------------------------------------------
# Paths, config, and Compose parsing
# ---------------------------------------------------------------------------------------------------


@dataclass
class Settings:
    config_dir: Path
    config_file: Path | None
    compose_file: Path
    swagger_dir: Path
    config: dict


def resolve_settings(args: argparse.Namespace) -> Settings:
    config_dir_value = args.config_dir or os.environ.get(ENV_CONFIG_DIR)
    config_dir = (
        Path(config_dir_value).expanduser().resolve()
        if config_dir_value
        else DEFAULT_CONFIG_DIR
    )
    if not config_dir.is_dir():
        raise SetupError(f"Config directory does not exist: {fpath(config_dir, sys.stderr)}")

    explicit_config = args.config or os.environ.get(ENV_CONFIG_FILE)
    if explicit_config:
        config_file: Path | None = Path(explicit_config).expanduser().resolve()
        if not config_file.is_file():
            raise SetupError(f"Config file does not exist: {fpath(config_file, sys.stderr)}")
    else:
        candidate = config_dir / CONFIG_FILE_NAME
        config_file = candidate if candidate.is_file() else None

    config: dict = {}
    if config_file:
        try:
            loaded = yaml.safe_load(config_file.read_text()) or {}
        except yaml.YAMLError as exc:
            raise SetupError(
                f"Unable to parse config file {fpath(config_file, sys.stderr)}: {exc}"
            ) from exc
        if not isinstance(loaded, dict):
            raise SetupError(f"Config file {fpath(config_file, sys.stderr)} must contain a mapping")
        config = loaded

    explicit_compose = args.compose_file or os.environ.get(ENV_COMPOSE_FILE)
    if explicit_compose:
        compose_file = Path(explicit_compose).expanduser().resolve()
        if not compose_file.is_file():
            raise SetupError(f"Compose file does not exist: {fpath(compose_file, sys.stderr)}")
    else:
        found = [
            config_dir / name
            for name in COMPOSE_FILE_NAMES
            if (config_dir / name).is_file()
        ]
        if not found:
            raise SetupError(
                f"No Compose file found in {fpath(config_dir, sys.stderr)} (looked for {', '.join(COMPOSE_FILE_NAMES)})"
            )
        compose_file = found[0]

    swagger_value = args.swagger_dir or os.environ.get(ENV_SWAGGER_DIR)
    # Default swagger/ is at the repository root, two levels above test/local/.
    swagger_dir = (
        Path(swagger_value).expanduser().resolve()
        if swagger_value
        else (config_dir / ".." / ".." / "swagger").resolve()
    )
    if not swagger_dir.is_dir():
        raise SetupError(f"Swagger directory does not exist: {fpath(swagger_dir, sys.stderr)}")

    return Settings(config_dir, config_file, compose_file, swagger_dir, config)


_INTERPOLATION = re.compile(r"\$\{([A-Za-z_][A-Za-z0-9_]*)(?:(:?-)([^}]*))?\}")


def interpolate(value: str) -> str:
    """Apply the common Compose ${VAR}, ${VAR-default} and ${VAR:-default} forms."""

    def replace(match: re.Match) -> str:
        name, operator, default = match.group(1), match.group(2), match.group(3) or ""
        current = os.environ.get(name)
        if operator == ":-" and not current:
            return default
        if operator == "-" and current is None:
            return default
        return current or ""

    return _INTERPOLATION.sub(replace, value)


def load_compose(compose_file: Path) -> dict:
    try:
        compose = yaml.safe_load(compose_file.read_text()) or {}
    except yaml.YAMLError as exc:
        raise SetupError(f"Unable to parse Compose file {fpath(compose_file, sys.stderr)}: {exc}") from exc
    if not isinstance(compose.get("services"), dict):
        raise SetupError(f"Compose file {fpath(compose_file, sys.stderr)} has no services")
    return compose


def find_service(
    compose: dict, name: str, image_hint: str | None = None
) -> tuple[str, dict]:
    services = compose["services"]
    if name in services:
        return name, services[name]
    if image_hint:
        for service_name, service in services.items():
            if image_hint in str(service.get("image", "")):
                return service_name, service
    raise SetupError(f"Compose file has no {name!r} service")


def published_port(service_name: str, service: dict) -> int:
    for entry in service.get("ports") or []:
        if isinstance(entry, dict):
            published = entry.get("published")
            if published is not None:
                return int(interpolate(str(published)).split("-")[0])
            continue
        text = interpolate(str(entry)).split("/")[0]
        parts = text.split(":")
        # Forms: "8080" (container only), "9000:8080", "127.0.0.1:9000:8080".
        if len(parts) >= 2 and parts[-2]:
            return int(parts[-2].split("-")[0])
    raise SetupError(f"Compose service {service_name!r} publishes no host port")


def keycloak_realm_files(compose_file: Path, service: dict) -> list[Path]:
    files = []
    for volume in service.get("volumes") or []:
        if isinstance(volume, dict):
            source = volume.get("source")
        else:
            source = str(volume).split(":")[0]
        if source and str(source).endswith(".json"):
            path = (compose_file.parent / interpolate(str(source))).resolve()
            if path.is_file():
                files.append(path)
    return files


def check_port(port: int, label: str) -> None:
    try:
        with socket.create_connection(("localhost", port), timeout=3):
            return
    except OSError as exc:
        raise PortClosedError(
            f"{label} port {port} is not open on localhost ({exc})"
        ) from exc


# ---------------------------------------------------------------------------------------------------
# Authentication
# ---------------------------------------------------------------------------------------------------


@dataclass
class AuthSettings:
    token_url: str
    client_id: str
    username: str
    password: str


def resolve_auth(
    config: dict, keycloak_port: int, realm_files: list[Path]
) -> AuthSettings:
    overrides = config.get("auth") or {}
    username = overrides.get("username") or DEFAULT_USERNAME
    realm = client_id = password = None

    for realm_file in realm_files:
        try:
            data = json.loads(realm_file.read_text())
        except (OSError, json.JSONDecodeError):
            continue
        realm = realm or data.get("realm")
        for client in data.get("clients") or []:
            if (
                client.get("publicClient")
                and client.get("directAccessGrantsEnabled")
                and not client_id
            ):
                client_id = client.get("clientId")
        for user in data.get("users") or []:
            if user.get("username") == username and not password:
                for credential in user.get("credentials") or []:
                    if credential.get("type") == "password":
                        password = credential.get("value")

    realm = overrides.get("realm") or realm or DEFAULT_REALM
    client_id = overrides.get("client_id") or client_id
    password = overrides.get("password") or password or DEFAULT_PASSWORD
    if not client_id:
        raise SetupError(
            "Unable to find a public Keycloak client with direct access grants in the realm import file. "
            "Set auth.client_id in the config file."
        )
    token_url = overrides.get("token_url") or (
        f"http://localhost:{keycloak_port}/realms/{realm}/protocol/openid-connect/token"
    )
    return AuthSettings(token_url, client_id, username, password)


class TokenProvider:
    def __init__(self, settings: AuthSettings):
        self.settings = settings
        self.token: str | None = None
        self.expires_at = 0.0

    def get(self, force: bool = False) -> str:
        if force or not self.token or time.time() > self.expires_at - 30:
            self._refresh()
        assert self.token
        return self.token

    def _refresh(self) -> None:
        form = {
            "grant_type": "password",
            "client_id": self.settings.client_id,
            "username": self.settings.username,
            "password": self.settings.password,
        }
        request = urllib.request.Request(
            self.settings.token_url,
            data=urllib.parse.urlencode(form).encode("utf-8"),
            method="POST",
            headers={"Content-Type": "application/x-www-form-urlencoded"},
        )
        try:
            with urllib.request.urlopen(
                request, timeout=HTTP_TIMEOUT_SECONDS
            ) as response:
                payload = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise SetupError(
                f"Token request to {self.settings.token_url} as {self.settings.username!r} failed "
                f"({exc.code}): {truncate(detail)}"
            ) from exc
        except urllib.error.URLError as exc:
            raise SetupError(
                f"Unable to reach Keycloak at {self.settings.token_url}: {exc.reason}"
            ) from exc
        self.token = payload["access_token"]
        self.expires_at = time.time() + float(payload.get("expires_in", 60))


# ---------------------------------------------------------------------------------------------------
# HTTP client
# ---------------------------------------------------------------------------------------------------


@dataclass
class Response:
    status: int
    body: Any
    text: str
    # Response headers with lower-case names.
    headers: dict[str, str] = field(default_factory=dict)

    @property
    def ok(self) -> bool:
        return 200 <= self.status < 300

    def describe(self) -> str:
        if isinstance(self.body, dict):
            errors = self.body.get("errors")
            title = " - ".join(
                str(part)
                for part in (self.body.get("title"), self.body.get("detail"))
                if part
            )
            if errors:
                return f"HTTP {self.status} {title} {json.dumps(errors)}".strip()
            if title:
                return f"HTTP {self.status} {title}"
        return f"HTTP {self.status} {truncate(self.text, 200)}".strip()


def problem_details_issue(response: Response) -> str | None:
    """Describe how a 4xx body falls short of RFC 9457 ProblemDetails, or None when it complies."""
    if not response.text.strip():
        return "empty body"
    if not isinstance(response.body, dict) or not {"status", "title"} <= set(
        response.body
    ):
        return "body has no status or title"
    if response.body.get("status") != response.status:
        return f"body status is {response.body.get('status')!r}"
    return None


def problem_content_type_issue(response: Response) -> str | None:
    """RFC 9457 media type check, reported as a note rather than a failure."""
    content_type = response.headers.get("content-type", "")
    if "application/problem+json" in content_type:
        return None
    return f"content type {content_type.split(';')[0] or '(none)'}"


def tampered_token(token: str) -> str:
    """The same JWT with its signature replaced, so only signature validation can reject it."""
    header, _, rest = token.partition(".")
    payload, _, signature = rest.partition(".")
    return f"{header}.{payload}.{'A' * max(len(signature), 16)}"


def retry_after_seconds(value: str | None) -> float | None:
    if not value:
        return None
    try:
        return max(0.0, float(value))
    except ValueError:
        pass
    try:
        parsed = email.utils.parsedate_to_datetime(value)
    except (TypeError, ValueError):
        return None
    return max(0.0, (parsed - dt.datetime.now(dt.timezone.utc)).total_seconds())


class ApiClient:
    def __init__(self, base_url: str, tokens: TokenProvider):
        self.base_url = base_url.rstrip("/")
        self.tokens = tokens
        # (METHOD, path template) of every operation called, for the end report.
        self.endpoints_called: set[tuple[str, str]] = set()
        # Callables given (operation, response) after every call.
        self.observers: list[Any] = []
        # Rate-limited (429) responses that had no Retry-After header, or a Retry-After of 0.
        self.retry_after_missing = 0
        self.retry_after_zero = 0

    def call(
        self,
        operation: "Operation",
        path_values: dict[str, Any] | None = None,
        query: dict[str, Any] | None = None,
        body: Any = None,
        raw_body: bytes | None = None,
        auth: str = AUTH_TOKEN,
    ) -> Response:
        path = operation.path
        for name, value in (path_values or {}).items():
            path = path.replace(
                "{" + name + "}", urllib.parse.quote(str(value), safe="")
            )
        url = self.base_url + path
        if query:
            pairs = []
            for key, value in query.items():
                if value is None:
                    continue
                for item in value if isinstance(value, list) else [value]:
                    pairs.append(
                        (
                            key,
                            str(item).lower() if isinstance(item, bool) else str(item),
                        )
                    )
            if pairs:
                url += "?" + urllib.parse.urlencode(pairs)
        headers = {"Accept": "application/json"}
        for parameter in operation.header_params:
            # Idempotency-Key and similar request headers get a fresh unique value per call.
            headers[parameter] = str(uuid.uuid4())
        data = None
        if raw_body is not None:
            data = raw_body
            headers["Content-Type"] = "application/json"
        elif body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        self.endpoints_called.add((operation.method.upper(), operation.path))
        response = self._send(operation.method.upper(), url, headers, data, auth)
        for observer in self.observers:
            observer(operation, response)
        return response

    def _send(
        self,
        method: str,
        url: str,
        headers: dict[str, str],
        data: bytes | None,
        auth: str = AUTH_TOKEN,
    ) -> Response:
        backoff = 1.0
        refreshed = False
        for attempt in range(1, MAX_RETRY_ATTEMPTS + 1):
            if auth == AUTH_TOKEN:
                headers["Authorization"] = f"Bearer {self.tokens.get()}"
            elif auth == AUTH_GARBLED:
                headers["Authorization"] = "Bearer not-a-real-token"
            elif auth == AUTH_TAMPERED:
                headers["Authorization"] = f"Bearer {tampered_token(self.tokens.get())}"
            else:
                headers.pop("Authorization", None)
            request = urllib.request.Request(
                url, data=data, method=method, headers=headers
            )
            debug(
                f"{method} {url}" + (f" {truncate(data.decode(), 400)}" if data else "")
            )
            try:
                with urllib.request.urlopen(
                    request, timeout=HTTP_TIMEOUT_SECONDS
                ) as raw:
                    status, text, retry_after = (
                        raw.status,
                        raw.read().decode("utf-8", "replace"),
                        None,
                    )
                    response_headers = {k.lower(): v for k, v in raw.headers.items()}
            except urllib.error.HTTPError as exc:
                status = exc.code
                text = exc.read().decode("utf-8", "replace")
                retry_after = exc.headers.get("Retry-After")
                response_headers = {k.lower(): v for k, v in exc.headers.items()}
            except urllib.error.URLError as exc:
                return Response(0, None, f"connection failed: {exc.reason}")
            if status == 429 and retry_after is None:
                self.retry_after_missing += 1
            if status == 401 and not refreshed and auth == AUTH_TOKEN:
                self.tokens.get(force=True)
                refreshed = True
                continue
            if status == 429 and attempt < MAX_RETRY_ATTEMPTS:
                advised = retry_after_seconds(retry_after)
                if advised is not None and advised < 1:
                    self.retry_after_zero += 1
                # Retry-After is a floor, and backoff still doubles on repeated 429s. The example API
                # (2026-10-23) advised Retry-After 0-3 while its window stayed closed, which used up every
                # retry within seconds when Retry-After alone set the delay.
                delay = max(advised or 0.0, backoff)
                backoff = min(backoff * 2, MAX_BACKOFF_SECONDS)
                delay = min(delay, MAX_BACKOFF_SECONDS)
                info(
                    f"    {colour('rate limited (429)', 'yellow')}, "
                    f"waiting {delay:.0f}s before retrying"
                )
                time.sleep(delay)
                continue
            try:
                body = json.loads(text) if text.strip() else None
            except json.JSONDecodeError:
                body = None
            debug(f"-> {status} {truncate(text, 400)}")
            return Response(status, body, text, response_headers)
        return Response(429, None, "rate limited: retries exhausted")


# ---------------------------------------------------------------------------------------------------
# OpenAPI model
# ---------------------------------------------------------------------------------------------------


class Spec:
    def __init__(self, document: dict):
        self.document = document
        self.schemas = (document.get("components") or {}).get("schemas") or {}

    def deref(self, schema: dict | None) -> dict:
        """Follow $ref and the single-member allOf/oneOf/anyOf wrappers NSwag uses for nullable refs."""
        seen = 0
        schema = schema or {}
        while seen < 32:
            seen += 1
            if "$ref" in schema:
                schema = self.schemas.get(schema["$ref"].split("/")[-1], {})
                continue
            for key in ("allOf", "oneOf", "anyOf"):
                members = schema.get(key)
                if members and len(members) == 1 and not schema.get("properties"):
                    schema = members[0]
                    break
            else:
                return schema
        return schema

    def identity(self, schema: dict | None) -> str | None:
        """Return the component a schema points at, used to recognize identical models."""
        schema = schema or {}
        while True:
            if "$ref" in schema:
                return schema["$ref"]
            members = schema.get("allOf") or schema.get("oneOf") or schema.get("anyOf")
            if members and len(members) == 1:
                schema = members[0]
                continue
            return None

    def fields(self, schema: dict | None) -> dict[str, dict]:
        schema = self.deref(schema)
        result: dict[str, dict] = {}
        for member in schema.get("allOf") or []:
            result.update(self.fields(member))
        result.update(schema.get("properties") or {})
        return result

    def is_object(self, schema: dict | None) -> bool:
        resolved = self.deref(schema)
        return bool(self.fields(resolved)) or resolved.get("type") == "object"

    def array_item(self, schema: dict | None) -> dict | None:
        resolved = self.deref(schema)
        return resolved.get("items") if resolved.get("type") == "array" else None

    def kind(self, schema: dict | None) -> str:
        resolved = self.deref(schema)
        if "enum" in resolved:
            return "enum"
        schema_type = resolved.get("type")
        schema_format = resolved.get("format")
        if schema_type == "string":
            if schema_format in ("uuid", "guid"):
                return "guid"
            if schema_format in BINARY_FORMATS:
                return "binary"
            return {"date-time": "datetime", "date": "date"}.get(
                schema_format, "string"
            )
        if schema_type in ("integer", "number", "boolean", "array"):
            return schema_type
        if self.fields(resolved) or schema_type == "object":
            return "object"
        return "unknown"

    def contains_binary(self, schema: dict | None, depth: int = 0) -> bool:
        if depth > 4 or schema is None:
            return False
        if self.kind(schema) == "binary":
            return True
        item = self.array_item(schema)
        if item is not None:
            return self.contains_binary(item, depth + 1)
        return any(
            self.contains_binary(child, depth + 1)
            for child in self.fields(schema).values()
        )


def parse_datetime(value: Any) -> dt.datetime | None:
    """Parse an ISO 8601 timestamp. .NET may send 7 fractional digits, and no zone means UTC."""
    if not isinstance(value, str):
        return None
    text = value.strip()
    if text.endswith(("Z", "z")):
        text = text[:-1] + "+00:00"
    match = re.match(r"^(.+T\d{2}:\d{2}:\d{2})(\.\d+)?(.*)$", text)
    if match:
        # Python accepts at most 6 fractional digits.
        text = match.group(1) + (match.group(2) or "")[:7] + match.group(3)
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return None
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=dt.timezone.utc)


class SchemaValidator:
    """Checks a JSON value against an OpenAPI schema: types, formats, enums, required, and nullable."""

    def __init__(self, spec: Spec):
        self.spec = spec

    def nullable(self, schema: dict | None) -> bool:
        schema = schema or {}
        for _ in range(32):
            if schema.get("nullable"):
                return True
            if "$ref" in schema:
                schema = self.spec.schemas.get(schema["$ref"].split("/")[-1], {})
                continue
            members = schema.get("allOf") or schema.get("oneOf") or schema.get("anyOf")
            if members and any(member.get("type") == "null" for member in members):
                return True
            if members and len(members) == 1 and not schema.get("properties"):
                schema = members[0]
                continue
            return False
        return False

    def required(self, schema: dict | None) -> set[str]:
        resolved = self.spec.deref(schema)
        names = set(resolved.get("required") or [])
        for member in resolved.get("allOf") or []:
            names |= self.required(member)
        return names

    def validate(self, schema: dict | None, value: Any) -> list[str]:
        issues: list[str] = []
        self._check(schema, value, "$", issues, 0)
        return issues

    def _check(
        self, schema: dict | None, value: Any, path: str, issues: list[str], depth: int
    ) -> None:
        if depth > 12 or len(issues) >= MAX_ISSUES_SHOWN:
            return
        if value is None:
            if not self.nullable(schema):
                issues.append(f"{path} is null but not nullable")
            return
        resolved = self.spec.deref(schema)
        kind = self.spec.kind(resolved)
        found = type(value).__name__
        if kind == "enum":
            allowed = [
                str(item)
                for item in (resolved.get("enum") or [])
                + (resolved.get("x-enumNames") or [])
            ]
            if str(value) not in allowed:
                issues.append(f"{path} {value!r} is not an enum value")
        elif kind in ("string", "guid", "datetime", "date", "binary"):
            if not isinstance(value, str):
                issues.append(f"{path} is {found}, expected string")
            elif kind == "guid" and not GUID_PATTERN.match(value):
                issues.append(f"{path} {value!r} is not a GUID")
            elif kind == "datetime" and parse_datetime(value) is None:
                issues.append(f"{path} {value!r} is not a date-time")
        elif kind == "integer":
            if isinstance(value, bool) or not isinstance(value, int):
                issues.append(f"{path} is {found}, expected integer")
        elif kind == "number":
            if isinstance(value, bool) or not isinstance(value, (int, float)):
                issues.append(f"{path} is {found}, expected number")
        elif kind == "boolean":
            if not isinstance(value, bool):
                issues.append(f"{path} is {found}, expected boolean")
        elif kind == "array":
            if not isinstance(value, list):
                issues.append(f"{path} is {found}, expected array")
            else:
                item = self.spec.array_item(resolved)
                for index, child in enumerate(value[:20]):
                    self._check(item, child, f"{path}[{index}]", issues, depth + 1)
        elif kind == "object":
            if not isinstance(value, dict):
                issues.append(f"{path} is {found}, expected object")
                return
            for name in sorted(self.required(resolved)):
                if name not in value:
                    issues.append(f"{path}.{name} is required but missing")
            for name, child in self.spec.fields(resolved).items():
                if name in value:
                    self._check(child, value[name], f"{path}.{name}", issues, depth + 1)


class ResponseAudit:
    """Watches every API response for spec drift, ProblemDetails use, and rate-limit headers."""

    def __init__(self, spec: Spec):
        self.validator = SchemaValidator(spec)
        self.spec_issues: list[str] = []
        self.problem_issues: list[str] = []
        self.content_type_issues: list[str] = []
        self.rate_limit_issues: list[str] = []
        self.client_errors = 0
        self.responses = 0
        self.rate_limit_headers: set[str] = set()

    def __call__(self, operation: "Operation", response: Response) -> None:
        self.responses += 1
        for name in response.headers:
            if name.startswith(RATE_LIMIT_HEADER_PREFIXES):
                self.rate_limit_headers.add(name)
        if response.ok and operation.response_schema is not None:
            if response.body is None:
                if response.status != 204:
                    self.spec_issues.append(
                        f"{operation.label}: HTTP {response.status} has no JSON body, "
                        "the spec declares one"
                    )
            else:
                for issue in self.validator.validate(
                    operation.response_schema, response.body
                ):
                    self.spec_issues.append(f"{operation.label}: {issue}")
        if response.status == 429:
            issue = problem_details_issue(response)
            if issue:
                self.rate_limit_issues.append(f"{operation.label}: 429 {issue}")
        # 401 is the authentication handler's bare challenge (WWW-Authenticate), not an API error body.
        # 429 is reported as a note (above) while rate-limit behaviour is not yet held to the standard.
        if 400 <= response.status < 500 and response.status not in (401, 429):
            self.client_errors += 1
            issue = problem_details_issue(response)
            if issue:
                self.problem_issues.append(
                    f"{operation.label} HTTP {response.status}: {issue}"
                )
            else:
                issue = problem_content_type_issue(response)
                if issue:
                    self.content_type_issues.append(
                        f"{operation.label} HTTP {response.status}: {issue}"
                    )

    def take(self) -> tuple[list[str], list[str], list[str], int]:
        """Return and reset what was seen since the last call."""
        taken = (
            list(dict.fromkeys(self.spec_issues)),
            list(dict.fromkeys(self.problem_issues)),
            list(dict.fromkeys(self.content_type_issues)),
            self.client_errors,
        )
        self.spec_issues, self.problem_issues = [], []
        self.content_type_issues, self.client_errors = [], 0
        return taken


def summarize(issues: list[str]) -> str:
    shown = "; ".join(issues[:MAX_ISSUES_SHOWN])
    hidden = len(issues) - MAX_ISSUES_SHOWN
    return shown + (f"; and {hidden} more" if hidden > 0 else "")


def lowered(value: Any) -> dict[str, Any]:
    return {norm(k): v for k, v in value.items()} if isinstance(value, dict) else {}


def overlap(source: dict[str, dict], target: dict[str, dict]) -> float:
    """Fraction of source field names that also appear in target (case-insensitive)."""
    if not source:
        return 0.0
    target_names = {norm(name) for name in target}
    return sum(1 for name in source if norm(name) in target_names) / len(source)


@dataclass
class Operation:
    method: str
    path: str
    tag: str
    operation_id: str
    path_params: list[str]
    phantom_path_params: list[str]
    path_param_schemas: dict[str, dict]
    query_params: dict[str, dict]
    required_query: list[str]
    header_params: list[str]
    request_content_types: list[str]
    body_schema: dict | None
    response_schema: dict | None

    @property
    def label(self) -> str:
        return f"{self.method.upper()} {self.path}"


def load_operations(spec: Spec) -> list[Operation]:
    operations = []
    for path, path_item in (spec.document.get("paths") or {}).items():
        shared = path_item.get("parameters") or []
        template_names = re.findall(r"\{([^}]+)\}", path)
        for method in HTTP_METHODS:
            raw = path_item.get(method)
            if not raw:
                continue
            tags = raw.get("tags") or []
            operation_id = raw.get("operationId") or f"{method}:{path}"
            tag = tags[0] if tags else operation_id.split("_")[0]
            path_params, phantom, query, required_query, headers = [], [], {}, [], []
            path_schemas: dict[str, dict] = {}
            for parameter in shared + (raw.get("parameters") or []):
                if "$ref" in parameter:
                    parameter = spec.deref(parameter)
                location, name = parameter.get("in"), parameter.get("name")
                schema = parameter.get("schema") or {}
                if location == "path":
                    # Some generated specs list path parameters that are not in the template
                    # (World API ItemEquipmentStat_Patch itemId, 2026-10-23). Those cannot be sent.
                    if name in template_names:
                        path_params.append(name)
                        path_schemas[name] = schema
                    else:
                        phantom.append(name)
                elif location == "query":
                    query[name] = schema
                    if parameter.get("required"):
                        required_query.append(name)
                elif location == "header":
                    headers.append(name)
            content = (raw.get("requestBody") or {}).get("content") or {}
            body_schema = next(
                (
                    value.get("schema")
                    for key, value in content.items()
                    if "json" in key
                ),
                None,
            )
            response_schema = None
            for status, response in sorted((raw.get("responses") or {}).items()):
                if str(status).startswith("2"):
                    for key, value in (response.get("content") or {}).items():
                        if "json" in key and value.get("schema"):
                            response_schema = value["schema"]
                            break
                if response_schema:
                    break
            path_params.sort(key=template_names.index)
            operations.append(
                Operation(
                    method,
                    path,
                    tag,
                    operation_id,
                    path_params,
                    phantom,
                    path_schemas,
                    query,
                    required_query,
                    headers,
                    list(content.keys()),
                    body_schema,
                    response_schema,
                )
            )
    return operations


# ---------------------------------------------------------------------------------------------------
# Controller classification
# ---------------------------------------------------------------------------------------------------

SHAPE_CRUD = "basic CRUD"
SHAPE_COLLECTION = "collection save (special)"
SHAPE_NOT_CRUD = "not CRUD"
SHAPE_SKIPPED = "skipped"
SHAPE_IGNORED = "ignored (config)"

# Report status for controllers that are not tested.
UNTESTED_STATUS = {
    SHAPE_NOT_CRUD: "N/A",
    SHAPE_SKIPPED: "SKIPPED",
    SHAPE_IGNORED: "IGNORED",
}


@dataclass
class Controller:
    name: str
    operations: list[Operation]
    shape: str = SHAPE_NOT_CRUD
    reason: str = ""
    # Brief reason shown with the shape in the report, like "ignored (config)".
    summary: str = ""
    get_by_id: Operation | None = None
    create: Operation | None = None
    put: Operation | None = None
    patch: Operation | None = None
    delete: Operation | None = None
    list_op: Operation | None = None
    list_array_field: str | None = None
    # Collection save special case: write array field -> read array field.
    collection_write_field: str | None = None
    collection_read_field: str | None = None
    notes: list[str] = field(default_factory=list)

    @property
    def read_schema(self) -> dict | None:
        return self.get_by_id.response_schema if self.get_by_id else None

    @property
    def put_is_child(self) -> bool:
        """PUT addressed by path parameter with no POST: the record is keyed by its parent."""
        return bool(self.put and self.put.path_params and not self.create)

    def variant(self) -> str:
        parts = []
        if self.create:
            parts.append("POST")
        if self.put:
            parts.append("PUT/{key}" if self.put.path_params else "PUT(upsert)")
        parts.append("GET")
        for label, operation in (
            ("PATCH", self.patch),
            ("LIST", self.list_op),
            ("DELETE", self.delete),
        ):
            if operation:
                parts.append(label)
        return "+".join(parts)


def binary_skip_reason(spec: Spec, controller: Controller) -> tuple[str, str] | None:
    """Return (brief summary, detail) when a controller carries file or binary payloads."""
    for operation in controller.operations:
        if operation.method not in ("post", "put", "patch"):
            continue
        for content_type in operation.request_content_types:
            if content_type.startswith(BINARY_CONTENT_TYPES):
                return "file upload", f"{operation.label} accepts {content_type}"
        if operation.body_schema and spec.contains_binary(operation.body_schema):
            return "binary payload", f"{operation.label} body has a binary/byte field"
        for name, schema in spec.fields(operation.body_schema).items():
            if norm(name) in BASE64_FIELD_NAMES and spec.kind(schema) == "string":
                return "base64 payload", f"{operation.label} body has base64 field {name!r}"
        # POST that creates without a JSON body: Upload in the example API (2026-10-23) streams the
        # raw file and is event-sourced, so it is RESTful but not basic CRUD.
        if (
            operation.method == "post"
            and not operation.path_params
            and operation.body_schema is None
        ):
            return "no JSON body", f"{operation.label} has no JSON request body"
    return None


def list_array_field(
    spec: Spec, operation: Operation, read_schema: dict | None
) -> str | None:
    """Name of the array property in a GET response whose items look like the read model."""
    if not operation.response_schema or read_schema is None:
        return None
    read_identity = spec.identity(read_schema)
    read_fields = spec.fields(read_schema)
    for name, schema in spec.fields(operation.response_schema).items():
        item = spec.array_item(schema)
        if item is None:
            continue
        if read_identity and spec.identity(item) == read_identity:
            return name
        if (
            read_fields
            and overlap(read_fields, spec.fields(item)) >= LIST_ITEM_OVERLAP_THRESHOLD
        ):
            return name
    return None


def classify(spec: Spec, controller: Controller, ignored: set[str]) -> None:
    if norm(controller.name) in ignored:
        controller.shape = SHAPE_IGNORED
        controller.reason = "listed in config ignore"
        return

    gets = [op for op in controller.operations if op.method == "get"]
    writes = [
        op
        for op in controller.operations
        if op.method in ("post", "put")
        and op.body_schema is not None
        and spec.is_object(op.body_schema)
    ]

    # GET-by-ID candidates: path-addressed GETs that return one object, not a list of other responses.
    other_models = [
        op.response_schema for op in controller.operations if op.response_schema
    ]
    candidates = []
    for op in gets:
        if not op.path_params or op.required_query or not op.response_schema:
            continue
        if not spec.is_object(op.response_schema):
            continue
        if any(
            list_array_field(spec, op, model)
            for model in other_models
            if model is not op.response_schema
        ):
            continue
        candidates.append(op)

    if not candidates:
        controller.shape = SHAPE_NOT_CRUD
        controller.reason = "no GET-by-ID endpoint"
        controller.summary = "no GET-by-ID"
        return
    if not writes:
        controller.shape = SHAPE_NOT_CRUD
        controller.reason = "no JSON POST/PUT endpoint"
        controller.summary = "no JSON write"
        return

    skip = binary_skip_reason(spec, controller)

    def write_similarity(op: Operation) -> float:
        response_fields = spec.fields(op.response_schema)
        return max(
            (overlap(spec.fields(w.body_schema), response_fields) for w in writes),
            default=0.0,
        )

    candidates.sort(
        key=lambda op: (-write_similarity(op), len(op.path_params), len(op.path))
    )
    controller.get_by_id = candidates[0]
    read_schema = controller.read_schema
    read_fields = spec.fields(read_schema)
    key_count = len(controller.get_by_id.path_params)

    for op in writes:
        if op.method == "post" and not op.path_params and controller.create is None:
            controller.create = op
        elif (
            op.method == "put"
            and controller.put is None
            and len(op.path_params) in (0, key_count)
        ):
            controller.put = op

    best_overlap = max(
        (
            overlap(spec.fields(op.body_schema), read_fields)
            for op in (controller.create, controller.put)
            if op
        ),
        default=0.0,
    )

    read_names = {norm(name) for name in read_fields}
    key_names = {norm(name) for name in controller.get_by_id.path_params}

    def patch_addressable(op: Operation) -> bool:
        """Same key count as GET-by-ID, or extra parent parameters the read model can fill.

        World API ItemEquipmentStat (2026-10-23) patches at /items/{itemId}/equipment/stat/{entryId}
        while its GET-by-ID is /items/attributes/equipment/stat/{entryId}.
        """
        if len(op.path_params) == key_count:
            return True
        extra = [norm(name) for name in op.path_params if norm(name) not in key_names]
        return len(op.path_params) > key_count and all(name in read_names for name in extra)

    patch_candidates = [
        op for op in controller.operations
        if op.method == "patch" and op.body_schema and patch_addressable(op)
    ]
    patch_candidates.sort(key=lambda op: len(op.path_params) != key_count)
    for op in controller.operations:
        if op is (patch_candidates[0] if patch_candidates else None):
            if (
                op.response_schema is None
                or overlap(read_fields, spec.fields(op.response_schema))
                >= PATCH_RESPONSE_OVERLAP_THRESHOLD
            ):
                controller.patch = op
            else:
                controller.notes.append(
                    f"{op.label} response does not resemble the read model, PATCH not tested"
                )
        if (
            op.method == "delete"
            and len(op.path_params) == key_count
            and controller.delete is None
        ):
            controller.delete = op

    list_candidates = []
    for op in gets:
        if op is controller.get_by_id:
            continue
        array_field = list_array_field(spec, op, read_schema)
        if array_field:
            list_candidates.append(
                (len(op.path_params), len(op.required_query), op, array_field)
            )
    if list_candidates:
        list_candidates.sort(key=lambda item: (item[0], item[1]))
        _, _, controller.list_op, controller.list_array_field = list_candidates[0]

    if skip:
        controller.shape = SHAPE_SKIPPED
        controller.summary, controller.reason = skip
        return

    if best_overlap >= WRITE_READ_OVERLAP_THRESHOLD and (
        controller.create or controller.put
    ):
        controller.shape = SHAPE_CRUD
        return

    # Collection save special case: the write model holds an array whose items resemble the items of an
    # array in the read model (Realm API CharacterInventory / CharacterActionBar, 2026-10-23).
    if controller.put and controller.put.path_params:
        for write_name, write_schema in spec.fields(controller.put.body_schema).items():
            write_item = spec.array_item(write_schema)
            if write_item is None:
                continue
            write_item_fields = spec.fields(write_item)
            for read_name, read_field_schema in read_fields.items():
                read_item = spec.array_item(read_field_schema)
                if read_item is None:
                    continue
                if (
                    overlap(write_item_fields, spec.fields(read_item))
                    >= COLLECTION_OVERLAP_THRESHOLD
                ):
                    controller.shape = SHAPE_COLLECTION
                    controller.collection_write_field = write_name
                    controller.collection_read_field = read_name
                    return

    controller.shape = SHAPE_NOT_CRUD
    controller.summary = "write/read mismatch"
    controller.reason = (
        f"write model shares {best_overlap:.0%} of its fields with the read model "
        "(possibly command-style or event-sourced)"
    )


# ---------------------------------------------------------------------------------------------------
# Value generation and comparison
# ---------------------------------------------------------------------------------------------------


class ValueFactory:
    def __init__(self, spec: Spec, run_tag: str, rng: random.Random):
        self.spec = spec
        self.run_tag = run_tag
        self.rng = rng
        self.counter = 0
        # Formats seen while generating, reported at the end.
        self.formats_seen: dict[str, int] = {}

    def unique(self) -> str:
        self.counter += 1
        return f"crt-{self.run_tag}-{self.counter}"

    def scalar(self, schema: dict, index: int | None = None, avoid: Any = None) -> Any:
        resolved = self.spec.deref(schema)
        kind = self.spec.kind(resolved)
        schema_format = resolved.get("format")
        if schema_format:
            self.formats_seen[schema_format] = (
                self.formats_seen.get(schema_format, 0) + 1
            )
        for _ in range(8):
            value = self._scalar_once(resolved, kind, index)
            if value != avoid:
                return value
        return value

    def _scalar_once(self, resolved: dict, kind: str, index: int | None) -> Any:
        schema_format = resolved.get("format")
        if kind == "enum":
            values = resolved["enum"]
            return self.rng.choice(values) if values else None
        if kind == "guid":
            return str(uuid.uuid4())
        if kind == "datetime":
            return (
                dt.datetime.now(dt.timezone.utc)
                .replace(microsecond=0)
                .isoformat()
                .replace("+00:00", "Z")
            )
        if kind == "date":
            return dt.date.today().isoformat()
        if kind == "string":
            if schema_format == "email":
                value = f"{self.unique()}@example.test"
            elif schema_format in ("uri", "url"):
                value = f"https://example.test/{self.unique()}"
            elif schema_format == "ipv4":
                value = "127.0.0.1"
            elif schema_format == "hostname":
                value = f"{self.unique()}.example.test"
            else:
                value = self.unique()
            max_length = resolved.get("maxLength")
            if max_length and len(value) > max_length:
                value = value[-max_length:]
            min_length = resolved.get("minLength") or 0
            return value.ljust(min_length, "x")
        if kind == "integer":
            # Without a format the server type is unknown. World API (2026-10-23) maps format-less integers
            # to Int16 / UInt16 / byte, so keep them small. int32/int64 get a wide range so upserts with
            # integer keys (World API Zones) do not land on existing records.
            wide = schema_format in ("int32", "int64", "uint32", "uint64")
            low = int(resolved.get("minimum", 1000 if wide else 1))
            high = int(resolved.get("maximum", 999_999 if wide else 120))
            if index is not None:
                # Collection items (slots and the like) get small distinct values: 1, 2, ...
                return min(max(int(resolved.get("minimum", 1)), index + 1), high)
            return self.rng.randint(low, high)
        if kind == "number":
            low = float(resolved.get("minimum", 1))
            high = float(resolved.get("maximum", 1000))
            return round(self.rng.uniform(low, high), 2)
        if kind == "boolean":
            return self.rng.choice([True, False])
        return None

    def generate(
        self,
        schema: dict | None,
        resolver: "DependencyResolver | None" = None,
        depth: int = 0,
        index: int | None = None,
        array_count: int = 1,
    ) -> Any:
        if schema is None or depth > 6:
            return None
        kind = self.spec.kind(schema)
        if kind == "object":
            result = {}
            for name, child in self.spec.fields(schema).items():
                if self.spec.deref(child).get("readOnly"):
                    continue
                has_sample, sample = (
                    resolver.sample(name) if resolver else (False, None)
                )
                linked = (
                    None
                    if has_sample or not resolver
                    else resolver.value_for(name, child)
                )
                if has_sample:
                    result[name] = sample
                elif linked is not None:
                    result[name] = linked
                else:
                    result[name] = self.generate(
                        child, resolver, depth + 1, index, array_count
                    )
            return result
        if kind == "array":
            item = self.spec.array_item(schema)
            # Only the outermost arrays get array_count items, nested ones get one.
            return [
                self.generate(item, resolver, depth + 1, i) for i in range(array_count)
            ]
        return self.scalar(schema, index)


def values_match(spec: Spec, schema: dict | None, sent: Any, received: Any) -> bool:
    if sent is None:
        return True
    resolved = spec.deref(schema)
    kind = spec.kind(resolved)
    if kind == "enum":
        names = resolved.get("x-enumNames") or []
        values = resolved.get("enum") or []
        lookup = {str(v): str(n) for v, n in zip(values, names)}
        return str(sent) == str(received) or lookup.get(str(sent)) == str(received)
    if kind in ("number", "integer"):
        try:
            return abs(float(sent) - float(received)) < 1e-6
        except (TypeError, ValueError):
            return False
    if kind == "guid":
        return str(sent).lower() == str(received).lower()
    if kind in ("datetime", "date"):
        return received is not None
    if kind in ("array", "object"):
        if kind == "array":
            return isinstance(received, list) and len(received) == len(sent)
        return isinstance(received, dict)
    return sent == received


def compare_record(
    spec: Spec, read_schema: dict | None, sent: dict, record: Any
) -> list[str]:
    """Return mismatch descriptions for sent fields that also exist in the read model."""
    if not isinstance(record, dict):
        return ["response is not a JSON object"]
    read_fields = {
        norm(name): (name, schema) for name, schema in spec.fields(read_schema).items()
    }
    received = {norm(name): value for name, value in record.items()}
    mismatches = []
    for name, value in sent.items():
        if norm(name) not in read_fields:
            continue
        _, schema = read_fields[norm(name)]
        if not values_match(spec, schema, value, received.get(norm(name))):
            mismatches.append(
                f"{name}: sent {value!r}, got {received.get(norm(name))!r}"
            )
    return mismatches


# ---------------------------------------------------------------------------------------------------
# Dependencies between controllers
# ---------------------------------------------------------------------------------------------------


def dependency_target(
    name: str,
    controllers: dict[str, Controller],
    own: str,
    config_links: dict[str, str],
) -> tuple[Controller | None, str | None]:
    """Find the controller a field or parameter name refers to.

    Detection (see DEPENDENCY_SUFFIXES): strip a known suffix, or use the bare name, and compare it to the
    controller tags. Falls back to the config file's dependencies section. Returns (controller, source).
    """
    lowered = norm(name)
    stems = [lowered]
    for suffix in DEPENDENCY_SUFFIXES:
        if lowered.endswith(norm(suffix)) and len(lowered) > len(suffix):
            stems.insert(0, lowered[: -len(suffix)])
    for stem in stems:
        target = controllers.get(stem)
        if target is not None and norm(target.name) != norm(own):
            return target, "detected"
    configured = config_links.get(lowered)
    if configured:
        target = controllers.get(norm(configured))
        if target is not None:
            return target, "config"
        warn(
            f"config dependency {own}.{name} -> {configured} names an unknown controller",
            once=True,
        )
    return None, None


class DependencyResolver:
    """Supplies key values for fields that reference another controller, creating parents on demand."""

    def __init__(self, runner: "Runner", controller: Controller):
        self.runner = runner
        self.controller = controller
        self.config_links = {
            norm(key): value
            for key, value in (
                (runner.config.get("dependencies") or {}).get(controller.name) or {}
            ).items()
        }
        # Sample values from the config file's values section, by field name. A list is cycled so
        # successive writes (create, update, patch) send different values.
        self.samples = {
            norm(key): value
            for key, value in (
                (runner.config.get("values") or {}).get(controller.name) or {}
            ).items()
        }
        self.sample_counters: dict[str, int] = {}
        self.used: dict[str, str] = {}
        self.missing: list[str] = []

    def sample(self, name: str) -> tuple[bool, Any]:
        lowered = norm(name)
        if lowered not in self.samples:
            return False, None
        value = self.samples[lowered]
        if isinstance(value, list):
            count = self.sample_counters.get(lowered, 0)
            self.sample_counters[lowered] = count + 1
            return True, value[count % len(value)] if value else None
        return True, value

    def target(self, name: str) -> tuple[Controller | None, str | None]:
        return dependency_target(
            name,
            self.runner.controllers_by_key,
            self.controller.name,
            self.config_links,
        )

    def value_for(
        self, name: str, schema: dict | None, random_fallback: bool = True
    ) -> Any:
        target, source = self.target(name)
        if target is None:
            return None
        key_schema = (
            target.get_by_id.path_param_schemas.get(target.get_by_id.path_params[0])
            if target.get_by_id
            else None
        )
        if schema is not None and key_schema is not None:
            spec = self.runner.spec
            if spec.kind(schema) != spec.kind(key_schema):
                return None
        value = self.runner.parent_key(target)
        if value is None:
            if random_fallback:
                self.missing.append(f"{name} -> {target.name}")
            return None
        self.used[name] = f"{target.name} ({source})"
        return value


# ---------------------------------------------------------------------------------------------------
# Pagination
# ---------------------------------------------------------------------------------------------------


class ContinuationTokenPagination:
    """Pages by echoing a response token field back as the query parameter of the same name.

    All list endpoints observed on 2026-10-23 use this. Add classes with the same interface and list them
    in PAGINATION_STRATEGIES to support other schemes.
    """

    def __init__(self, query_name: str, response_name: str):
        self.query_name = query_name
        self.response_name = response_name

    @classmethod
    def detect(
        cls, spec: Spec, operation: Operation
    ) -> "ContinuationTokenPagination | None":
        response_fields = {
            norm(name): name for name in spec.fields(operation.response_schema)
        }
        for query_name in operation.query_params:
            if norm(query_name) in response_fields and spec.kind(
                operation.query_params[query_name]
            ) in ("string", "guid"):
                return cls(query_name, response_fields[norm(query_name)])
        return None

    def pages(
        self, client: ApiClient, operation: Operation, path_values: dict, query: dict
    ) -> Iterator[Response]:
        token = None
        for _ in range(MAX_LIST_PAGES):
            params = dict(query)
            if token:
                params[self.query_name] = token
            response = client.call(operation, path_values, params)
            yield response
            if not response.ok or not isinstance(response.body, dict):
                return
            token = response.body.get(self.response_name)
            if not token:
                return


class SinglePagePagination:
    @classmethod
    def detect(cls, spec: Spec, operation: Operation) -> "SinglePagePagination":
        return cls()

    def pages(
        self, client: ApiClient, operation: Operation, path_values: dict, query: dict
    ) -> Iterator[Response]:
        yield client.call(operation, path_values, query)


PAGINATION_STRATEGIES = (ContinuationTokenPagination, SinglePagePagination)


def pagination_for(spec: Spec, operation: Operation):
    for strategy in PAGINATION_STRATEGIES:
        found = strategy.detect(spec, operation)
        if found is not None:
            return found
    return SinglePagePagination()


# ---------------------------------------------------------------------------------------------------
# Test execution
# ---------------------------------------------------------------------------------------------------

STATUS_PASS = "PASS"
STATUS_FAIL = "FAIL"


@dataclass
class Result:
    controller: Controller
    status: str = ""
    checks: list[tuple[str, bool, str]] = field(default_factory=list)
    endpoints: set[tuple[str, str]] = field(default_factory=set)
    notes: list[str] = field(default_factory=list)

    def check(self, label: str, ok: bool, detail: str = "") -> bool:
        self.checks.append((label, ok, detail))
        marker = colour("ok  ", "green") if ok else colour("FAIL", "red")
        info(f"    [{marker}] {label}" + (f": {detail}" if detail else ""))
        return ok

    def finish(self) -> None:
        if not self.status:
            self.status = (
                STATUS_PASS if all(ok for _, ok, _ in self.checks) else STATUS_FAIL
            )


@dataclass
class Record:
    controller: Controller
    keys: dict[str, Any]
    body: dict
    data: Any
    # Last PATCH body that succeeded, reused for the unknown-key PATCH check.
    patch_body: dict | None = None


class Runner:
    def __init__(
        self,
        spec: Spec,
        client: ApiClient,
        config: dict,
        controllers: list[Controller],
        keep: bool,
    ):
        self.spec = spec
        self.client = client
        self.config = config
        self.keep = keep
        self.controllers = controllers
        self.controllers_by_key = {norm(c.name): c for c in controllers}
        self.factory = ValueFactory(spec, uuid.uuid4().hex[:6], random.Random())
        self.parents: dict[str, Record | None] = {}
        self.parent_order: list[Record] = []
        self.parent_in_progress: set[str] = set()
        self.audit = ResponseAudit(spec)
        self.client.observers = [self.audit]
        self.retry_after_missing_at_start = client.retry_after_missing
        self.retry_after_zero_at_start = client.retry_after_zero

    # --- keys -------------------------------------------------------------------------------------

    def key_values(
        self,
        controller: Controller,
        record: Any,
        known: dict[str, Any],
        sent: dict | None = None,
    ) -> dict[str, Any]:
        """Map GET-by-ID path parameters to values for a written record.

        Order: values already used in the write path, a response field of the same name, a response "id",
        then the same two lookups in the sent body (World API NpcSpawnPoolSpawnPointIdleBehaviourGenericParameters,
        2026-10-23, answers with spawnPointId for a PUT keyed by body "id").
        """
        keys = {}
        sources = [
            {norm(name): value for name, value in source.items()}
            for source in (record, sent)
            if isinstance(source, dict)
        ]
        for position, name in enumerate(controller.get_by_id.path_params):
            if name in known:
                keys[name] = known[name]
                continue
            for lowered in sources:
                if norm(name) in lowered:
                    keys[name] = lowered[norm(name)]
                    break
                if "id" in lowered:
                    keys[name] = lowered["id"]
                    break
            else:
                if len(known) == len(controller.get_by_id.path_params):
                    keys[name] = list(known.values())[position]
        return keys

    def child_key_field(self, controller: Controller) -> str | None:
        """For a parent-keyed PUT, the read-model field that echoes the path key and names its parent.

        Realm API CharacterLocation (2026-10-23): PUT /{id} where CharacterLocationDto.characterId holds
        the same value and is not part of the write model.
        """
        key_name = controller.put.path_params[0]
        key_kind = self.spec.kind(controller.put.path_param_schemas.get(key_name))
        write_names = {
            norm(name) for name in self.spec.fields(controller.put.body_schema)
        }
        for name, schema in self.spec.fields(controller.read_schema).items():
            if norm(name) in write_names or self.spec.kind(schema) != key_kind:
                continue
            target, _ = dependency_target(
                name, self.controllers_by_key, controller.name, {}
            )
            if target is not None:
                return name
        return None

    def parent_path_values(
        self,
        controller: Controller,
        operation: Operation,
        resolver: DependencyResolver,
        result: Result | None,
    ) -> dict[str, Any] | None:
        """Path values for a write addressed by a parent key (child PUT or collection save)."""
        values = {}
        for name in operation.path_params:
            schema = operation.path_param_schemas.get(name)
            link = (
                name
                if resolver.target(name)[0]
                else (self.child_key_field(controller) if controller.put else None)
            )
            value = (
                resolver.value_for(link, schema, random_fallback=False)
                if link
                else None
            )
            if value is None:
                if result and link:
                    result.notes.append(
                        f"parent {resolver.target(link)[0].name} record for path parameter {name!r} could not be created"
                    )
                elif result:
                    result.notes.append(
                        f"cannot resolve parent for path parameter {name!r}; declare it under "
                        f"dependencies.{controller.name} in the config file"
                    )
                return None
            values[name] = value
        return values

    # --- writes -----------------------------------------------------------------------------------

    def write_new(
        self,
        controller: Controller,
        resolver: DependencyResolver,
        result: Result | None,
    ) -> Record | None:
        """Create one record with whatever create path the controller has."""
        if controller.create and not self.create_with_put(controller, resolver):
            body = self.factory.generate(controller.create.body_schema, resolver)
            response = self.client.call(controller.create, body=body)
            ok = response.ok and isinstance(response.body, dict)
            if result:
                result.check(
                    f"create {controller.create.label}",
                    ok,
                    "" if ok else response.describe(),
                )
            if not ok:
                return None
            return Record(
                controller,
                self.key_values(controller, response.body, {}, body),
                body,
                response.body,
            )

        operation = controller.put
        path_values: dict[str, Any] = {}
        if operation.path_params:
            found = self.parent_path_values(controller, operation, resolver, result)
            if found is None:
                if result:
                    result.check(
                        f"create {operation.label}",
                        False,
                        "no parent record for the path key",
                    )
                else:
                    warn(
                        f"{controller.name}: no parent record for the path key of {operation.label}"
                    )
                return None
            path_values = found
        body = self.factory.generate(operation.body_schema, resolver)
        response = self.client.call(operation, path_values, body=body)
        ok = response.ok
        if result:
            result.check(
                f"create {operation.label}", ok, "" if ok else response.describe()
            )
        if not ok:
            return None
        known = (
            dict(zip(controller.get_by_id.path_params, path_values.values()))
            if path_values
            else {}
        )
        data = response.body if isinstance(response.body, dict) else body
        return Record(
            controller, self.key_values(controller, data, known, body), body, data
        )

    def create_with_put(
        self, controller: Controller, resolver: DependencyResolver
    ) -> bool:
        """Prefer an upsert PUT over POST when only the PUT body can carry a parent link.

        World API NpcSpawnPoolSpawnPointIdleBehaviourGenericParameters (2026-10-23): the POST body has no
        parent field, while the PUT body "id" is the parent spawn point (linked through config).
        """
        put = controller.put
        if not put or put.path_params:
            return False
        post_fields = {
            norm(name) for name in self.spec.fields(controller.create.body_schema)
        }
        return any(
            resolver.target(name)[0] is not None
            for name in self.spec.fields(put.body_schema)
            if norm(name) not in post_fields
        )

    def parent_key(self, target: Controller) -> Any:
        """Create (once per run) a parent record for a dependency and return its key value."""
        key = norm(target.name)
        if key in self.parents:
            record = self.parents[key]
            return next(iter(record.keys.values()), None) if record else None
        if target.shape != SHAPE_CRUD or key in self.parent_in_progress:
            return None
        self.parent_in_progress.add(key)
        try:
            info(f"    creating parent {target.name} record")
            resolver = DependencyResolver(self, target)
            record = self.write_new(target, resolver, None)
        finally:
            self.parent_in_progress.discard(key)
        self.parents[key] = record
        if record:
            self.parent_order.append(record)
            return next(iter(record.keys.values()), None)
        warn(f"could not create parent {target.name} record")
        return None

    def cleanup_parents(self) -> None:
        if self.keep:
            return
        for record in reversed(self.parent_order):
            if not record.controller.delete:
                warn(
                    f"parent {record.controller.name} record {record.keys} left behind (no DELETE)"
                )
                continue
            response = self.client.call(
                record.controller.delete, self.delete_path(record)
            )
            if not response.ok:
                warn(
                    f"cleanup of parent {record.controller.name} {record.keys} failed: {response.describe()}"
                )

    def record_path_values(self, operation: Operation, record: Record) -> dict[str, Any]:
        """Path values for an operation addressing a written record.

        With the same parameter count as GET-by-ID the keys are used in order. Otherwise each parameter
        takes a key of the same name or the record field of the same name (a parent ID).
        """
        keys = list(record.keys.values())
        if len(operation.path_params) == len(keys):
            return dict(zip(operation.path_params, keys))
        named_keys = {norm(name): value for name, value in record.keys.items()}
        fields = (
            {norm(name): value for name, value in record.data.items()}
            if isinstance(record.data, dict)
            else {}
        )
        values = {}
        for name in operation.path_params:
            if norm(name) in named_keys:
                values[name] = named_keys[norm(name)]
            elif norm(name) in fields:
                values[name] = fields[norm(name)]
        return values

    def delete_path(self, record: Record) -> dict[str, Any]:
        delete = record.controller.delete
        return dict(zip(delete.path_params, record.keys.values()))

    # --- reads ------------------------------------------------------------------------------------

    def read(self, record: Record) -> Response:
        return self.client.call(record.controller.get_by_id, record.keys)

    def find_in_list(
        self, controller: Controller, record: Record, result: Result
    ) -> None:
        operation = controller.list_op
        unsatisfied = [name for name in operation.required_query]
        if unsatisfied:
            result.notes.append(
                f"{operation.label} needs query {unsatisfied}, list not tested"
            )
            return
        filters = self.list_key_filters(controller, record)
        found, pages, problem = (
            self.scan_list(controller, record, filters) if filters else (False, 0, "")
        )
        how = f"filtered by {sorted(filters)}"
        if not found and not problem:
            found, pages, problem = self.scan_list(controller, record, {})
            how = "unfiltered"
        detail = problem or f"{how}, {pages} page(s)"
        result.check(f"list {operation.label} contains record", found, detail)

    def list_path_values(self, controller: Controller, record: Record) -> dict[str, Any]:
        operation = controller.list_op
        if not operation.path_params:
            return {}
        # List scoped to a parent (World API ItemEquipmentStat_ListByItemId style).
        return self.record_path_values(operation, record)

    def list_key_filters(self, controller: Controller, record: Record) -> dict[str, Any]:
        """Query parameters that narrow a list to the record by its key.

        Uses parameters named after a key parameter, or after a record field holding a key value
        (Customer GET /{id} with search ?Id=, Item GET /{entryId} with ?Id=).
        """
        key_items = lowered(record.keys)
        record_fields = lowered(record.data)
        key_values = {str(v).lower() for v in record.keys.values()}
        key_fields = {
            name
            for name, value in record_fields.items()
            if str(value).lower() in key_values
        }
        filters = {}
        for name in controller.list_op.query_params:
            if norm(name) in key_items:
                filters[name] = key_items[norm(name)]
            elif norm(name) in key_fields:
                filters[name] = record_fields[norm(name)]
        return filters

    def scan_list(
        self, controller: Controller, record: Record, query: dict
    ) -> tuple[bool, int, str]:
        """Page through the list looking for the record. Returns (found, pages, problem)."""
        operation = controller.list_op
        pagination = pagination_for(self.spec, operation)
        path_values = self.list_path_values(controller, record)
        pages = 0
        for response in pagination.pages(self.client, operation, path_values, query):
            pages += 1
            if not response.ok or not isinstance(response.body, dict):
                return False, pages, response.describe()
            for item in lowered(response.body).get(norm(controller.list_array_field)) or []:
                if self.matches_keys(item, record):
                    return True, pages, ""
        return False, pages, ""

    def matches_keys(self, item: Any, record: Record) -> bool:
        if not isinstance(item, dict):
            return False
        lowered = {norm(k): v for k, v in item.items()}
        data = (
            {norm(k): v for k, v in record.data.items()}
            if isinstance(record.data, dict)
            else {}
        )
        for name, value in record.keys.items():
            candidates = [lowered.get(norm(name))]
            if "id" in lowered:
                candidates.append(lowered["id"])
            for field_name, field_value in data.items():
                if (
                    str(field_value).lower() == str(value).lower()
                    and field_name in lowered
                ):
                    candidates.append(lowered[field_name])
            if not any(
                str(c).lower() == str(value).lower()
                for c in candidates
                if c is not None
            ):
                return False
        return True

    # --- extended checks --------------------------------------------------------------------------

    def write_operation(self, controller: Controller, resolver: DependencyResolver) -> Operation:
        """The write the controller creates records with."""
        if controller.shape == SHAPE_COLLECTION:
            return controller.put
        if controller.create and not self.create_with_put(controller, resolver):
            return controller.create
        return controller.put

    def remove_unexpected(
        self, controller: Controller, response: Response, path_values: dict[str, Any]
    ) -> None:
        """Delete a record a negative check created by mistake, so it does not linger."""
        if not controller.delete or not isinstance(response.body, dict):
            return
        known = dict(zip(controller.get_by_id.path_params, path_values.values()))
        keys = self.key_values(controller, response.body, known)
        if keys:
            stray = Record(controller, keys, {}, response.body)
            self.client.call(controller.delete, self.delete_path(stray))

    def server_time_fields(self, controller: Controller) -> list[str]:
        """Read-model date-time fields that no write model sends, so the server manages them."""
        written: set[str] = set()
        for operation in (controller.create, controller.put, controller.patch):
            if operation:
                written |= {norm(name) for name in self.spec.fields(operation.body_schema)}
        return [
            name
            for name, schema in self.spec.fields(controller.read_schema).items()
            if self.spec.kind(schema) == "datetime" and norm(name) not in written
        ]

    def check_timestamps_current(
        self, controller: Controller, record: Record, result: Result
    ) -> None:
        data = record.data if isinstance(record.data, dict) else {}
        names = [name for name in self.server_time_fields(controller) if name in data]
        if not names:
            return
        now = dt.datetime.now(dt.timezone.utc)
        problems = []
        for name in names:
            parsed = parse_datetime(data.get(name))
            if parsed is None or abs((parsed - now).total_seconds()) > TIMESTAMP_SKEW_SECONDS:
                problems.append(f"{name}={data.get(name)!r}")
        result.check(
            "server timestamps are current",
            not problems,
            summarize(problems) or f"{len(names)} field(s)",
        )

    def wait_for_clock_tick(self, controller: Controller, record: Record) -> None:
        """Sleep past the next second when stored timestamps only keep whole seconds.

        Realm and World API timestamps are MariaDB DATETIME (2026-10-23), so an update within the same
        second would leave the "last changed" timestamp equal and look like it never moved.
        """
        data = record.data if isinstance(record.data, dict) else {}
        values = [
            data.get(name)
            for name in self.server_time_fields(controller)
            if isinstance(data.get(name), str)
        ]
        if values and not any(re.search(r"\.\d*[1-9]", value) for value in values):
            time.sleep(1.1)

    def check_timestamps_on_update(
        self, controller: Controller, before: Any, after: Any, result: Result, what: str
    ) -> None:
        """One server timestamp (creation) stays put and another (last change) moves forward."""
        before_data = before if isinstance(before, dict) else {}
        after_data = after if isinstance(after, dict) else {}
        pairs = {}
        for name in self.server_time_fields(controller):
            old, new = parse_datetime(before_data.get(name)), parse_datetime(after_data.get(name))
            if old is not None and new is not None:
                pairs[name] = (old, new)
        if not pairs:
            return
        constant = [name for name, (old, new) in pairs.items() if new == old]
        advanced = [name for name, (old, new) in pairs.items() if new > old]
        backwards = [name for name, (old, new) in pairs.items() if new < old]
        if len(pairs) == 1:
            # A single timestamp may record either creation or the last change, so only order is checked.
            ok = not backwards
        else:
            ok = bool(constant) and bool(advanced) and not backwards
        detail = f"unchanged {constant or '-'}, advanced {advanced or '-'}"
        if backwards:
            detail += f", went backwards {backwards}"
        result.check(f"server timestamps after {what}", ok, detail)

    def check_patch_integrity(
        self,
        controller: Controller,
        patched: str,
        before: Any,
        after: Any,
        result: Result,
    ) -> None:
        """Fields the PATCH did not name keep their values (server timestamps aside)."""
        before_data, after_data = lowered(before), lowered(after)
        skip = {norm(patched)} | {norm(name) for name in self.server_time_fields(controller)}
        changed = []
        for name, schema in self.spec.fields(controller.read_schema).items():
            key = norm(name)
            if key in skip or key not in before_data:
                continue
            old, new = before_data.get(key), after_data.get(key)
            same = json.dumps(old, sort_keys=True) == json.dumps(new, sort_keys=True) or (
                old is not None
                and new is not None
                and values_match(self.spec, schema, old, new)
            )
            if not same:
                changed.append(f"{name}: {old!r} -> {new!r}")
        result.check("PATCH leaves other fields unchanged", not changed, summarize(changed))

    def check_filters(self, controller: Controller, record: Record, result: Result) -> None:
        """Exact-match filters named after read-model fields return the record and only matches."""
        operation = controller.list_op
        pagination = pagination_for(self.spec, operation)
        skip = {norm(getattr(pagination, "query_name", "")), norm(self.page_size_param(controller) or "")}
        read_fields = {norm(name): schema for name, schema in self.spec.fields(controller.read_schema).items()}
        data = lowered(record.data)
        path_values = self.list_path_values(controller, record)
        tested, problems = [], []
        for name, query_schema in operation.query_params.items():
            key = norm(name)
            if key in skip or key not in read_fields or data.get(key) is None:
                continue
            field_schema = read_fields[key]
            if self.spec.kind(field_schema) not in ("string", "guid", "integer", "number", "enum", "boolean"):
                continue
            value = data[key]
            query = {name: [value] if self.spec.kind(query_schema) == "array" else value}
            found, wrong, pages = False, 0, 0
            for response in pagination.pages(self.client, operation, path_values, query):
                pages += 1
                if not response.ok or not isinstance(response.body, dict):
                    problems.append(f"{name}: {response.describe()}")
                    break
                for item in lowered(response.body).get(norm(controller.list_array_field)) or []:
                    if not values_match(self.spec, field_schema, value, lowered(item).get(key)):
                        wrong += 1
                    if self.matches_keys(item, record):
                        found = True
                if pages >= FILTER_MAX_PAGES:
                    break
            tested.append(name)
            if not found:
                problems.append(f"{name}={value!r} did not return the record")
            if wrong:
                problems.append(f"{name}={value!r} returned {wrong} non-matching record(s)")
        if tested:
            result.check(f"exact filters ({', '.join(tested)})", not problems, summarize(problems))

    def page_size_param(self, controller: Controller) -> str | None:
        """An integer query parameter not named after (or as a range on) a read-model field."""
        read_names = [norm(name) for name in self.spec.fields(controller.read_schema)]
        for name, schema in controller.list_op.query_params.items():
            if self.spec.kind(schema) != "integer":
                continue
            if any(norm(name).startswith(read_name) for read_name in read_names):
                continue
            return name
        return None

    def identity_field(self, record: Record) -> str | None:
        """The read-model field holding the record's first key value."""
        first = next(iter(record.keys.values()), None)
        for name, value in lowered(record.data).items():
            if first is not None and str(value).lower() == str(first).lower():
                return name
        return None

    def check_paging(
        self, controller: Controller, record: Record, resolver: DependencyResolver, result: Result
    ) -> None:
        """With a page size of 1, pages hold at most one record, never repeat one, and run out."""
        operation = controller.list_op
        pagination = pagination_for(self.spec, operation)
        size_param = self.page_size_param(controller)
        if not isinstance(pagination, ContinuationTokenPagination) or not size_param:
            result.notes.append(
                f"{operation.label} has no page-size and continuation-token pair, paging not checked"
            )
            return
        # A second record makes paging meaningful. It is always deleted afterwards, --keep or not,
        # because it only exists for this check.
        extra = None
        if controller.create and not self.create_with_put(controller, resolver):
            extra = self.write_new(controller, resolver, None)
        identity = self.identity_field(record)
        path_values = self.list_path_values(controller, record)
        seen: set[str] = set()
        repeated = oversized = pages = 0
        exhausted, problem, token = False, "", None
        for _ in range(PAGING_MAX_PAGES):
            query = {size_param: 1}
            if token:
                query[pagination.query_name] = token
            response = self.client.call(operation, path_values, query)
            pages += 1
            if not response.ok or not isinstance(response.body, dict):
                problem = response.describe()
                break
            body = lowered(response.body)
            items = body.get(norm(controller.list_array_field)) or []
            if len(items) > 1:
                oversized += 1
            for item in items:
                marker = json.dumps(lowered(item).get(identity) if identity else item, sort_keys=True)
                if marker in seen:
                    repeated += 1
                seen.add(marker)
            token = body.get(norm(pagination.response_name))
            if not token:
                exhausted = True
                break
        if extra and controller.delete:
            self.client.call(controller.delete, self.delete_path(extra))
        if problem:
            result.check(f"paging {operation.label} with {size_param}=1", False, problem)
            return
        if exhausted and len(seen) < 2:
            result.notes.append(f"{operation.label} listed fewer than 2 records, paging not exercised")
            return
        problems = []
        if oversized:
            problems.append(f"{oversized} page(s) held more than 1 record")
        if repeated:
            problems.append(f"{repeated} record(s) repeated across pages")
        detail = f"{pages} page(s), {len(seen)} record(s)"
        if not exhausted:
            detail += f", stopped after {PAGING_MAX_PAGES} pages"
        result.check(
            f"paging {operation.label} with {size_param}=1 is clean",
            not problems,
            summarize(problems) or detail,
        )

    def random_keys(self, controller: Controller) -> dict[str, Any]:
        operation = controller.get_by_id
        return {
            name: self.factory.scalar(
                operation.path_param_schemas.get(name) or {"type": "string", "format": "uuid"}
            )
            for name in operation.path_params
        }

    def unknown_keys(self, controller: Controller) -> tuple[dict[str, Any], Response]:
        """A random key and the GET-by-ID answer for it."""
        keys = self.random_keys(controller)
        response = self.client.call(controller.get_by_id, keys)
        if response.ok:
            # A random GUID matching a stored record is so unlikely it should never happen (random wide
            # integers and run-unique strings nearly so), but retry once with a fresh key before failing.
            keys = self.random_keys(controller)
            response = self.client.call(controller.get_by_id, keys)
        return keys, response

    def negative_sweep(
        self, controller: Controller, record: Record, resolver: DependencyResolver, result: Result
    ) -> None:
        keys, response = self.unknown_keys(controller)
        missing = response.status == 404
        result.check(
            f"GET unknown key {controller.get_by_id.label} is 404",
            missing,
            "" if missing else f"got HTTP {response.status}",
        )
        if missing:
            # Writes and deletes only use a key the GET just confirmed is absent.
            ghost = Record(controller, keys, {}, record.data)
            if controller.patch and record.patch_body:
                response = self.client.call(
                    controller.patch, self.record_path_values(controller.patch, ghost), body=record.patch_body
                )
                result.check(
                    f"PATCH unknown key {controller.patch.label} is 404",
                    response.status == 404,
                    "" if response.status == 404 else f"got HTTP {response.status}",
                )
            if controller.put and controller.put.path_params:
                path_values = self.record_path_values(controller.put, ghost)
                body = self.factory.generate(controller.put.body_schema, resolver)
                response = self.client.call(controller.put, path_values, body=body)
                if controller.create:
                    # Update-style PUT: 404, or create the record at that key (PUT as upsert is valid HTTP).
                    created = response.ok and all(
                        str(lowered(response.body).get(norm(name), value)).lower()
                        == str(value).lower()
                        for name, value in path_values.items()
                    )
                    ok, expected = response.status == 404 or created, "is 404 or creates it"
                    if created:
                        result.notes.append(
                            f"{controller.put.label} creates a record at an unknown key (upsert)"
                        )
                else:
                    # Parent-keyed upsert. Children are standalone tables in the current standard, so an
                    # unknown parent may be accepted. Only a server error fails (see future checks above).
                    ok, expected = response.status < 500, "is not a server error"
                result.check(
                    f"PUT unknown key {controller.put.label} {expected}",
                    ok,
                    "" if ok else response.describe(),
                )
                if response.ok and controller.delete:
                    self.client.call(controller.delete, self.delete_path(ghost))
            if controller.delete:
                response = self.client.call(controller.delete, self.delete_path(ghost))
                result.check(
                    f"DELETE unknown key {controller.delete.label} is 404",
                    response.status == 404,
                    "" if response.status == 404 else f"got HTTP {response.status}",
                )
        self.malformed_key_checks(controller, record, result)

    def malformed_key_checks(self, controller: Controller, record: Record, result: Result) -> None:
        """A GUID path parameter given a non-GUID answers 400 or 404, never 5xx or success."""
        problems, tried = [], 0
        for operation in (controller.get_by_id, controller.delete):
            if operation is None:
                continue
            values = self.record_path_values(operation, record)
            for name in operation.path_params:
                if self.spec.kind(operation.path_param_schemas.get(name)) != "guid":
                    continue
                tried += 1
                response = self.client.call(operation, dict(values, **{name: "not-a-guid"}))
                if response.status not in (400, 404):
                    problems.append(f"{operation.label} {name}: HTTP {response.status}")
        if tried:
            result.check(
                "malformed GUID keys are 400/404",
                not problems,
                summarize(problems) or f"{tried} request(s)",
            )

    def bad_body_checks(
        self, controller: Controller, record: Record, resolver: DependencyResolver, result: Result
    ) -> None:
        """Malformed JSON, a wrong-typed field, and an integer overflow answer 400 (or 422)."""
        operation = self.write_operation(controller, resolver)
        path_values = self.record_path_values(operation, record) if operation.path_params else {}
        valid = self.factory.generate(operation.body_schema, resolver)
        fields = self.spec.fields(operation.body_schema)
        cases: list[tuple[str, Any, bytes | None]] = [("malformed JSON", None, MALFORMED_JSON)]
        if fields and isinstance(valid, dict):
            name, schema = next(iter(fields.items()))
            kind = self.spec.kind(schema)
            wrong = 12345 if kind in ("string", "datetime", "date") else f"not-a-{kind}"
            cases.append((f"wrong type for {name}", dict(valid, **{name: wrong}), None))
            integers = [n for n, s in fields.items() if self.spec.kind(s) == "integer"]
            if integers:
                cases.append(
                    (
                        f"integer overflow for {integers[0]}",
                        dict(valid, **{integers[0]: INTEGER_OVERFLOW}),
                        None,
                    )
                )
        problems = []
        for label, body, raw in cases:
            response = self.client.call(operation, path_values, body=body, raw_body=raw)
            if response.status not in (400, 422):
                problems.append(f"{label}: {response.describe()}")
                if response.ok and not operation.path_params:
                    self.remove_unexpected(controller, response, path_values)
        result.check(
            f"bad request bodies to {operation.label} are 400",
            not problems,
            summarize(problems) or f"{len(cases)} case(s)",
        )

    def auth_checks(
        self, controller: Controller, record: Record, resolver: DependencyResolver, result: Result
    ) -> None:
        """No token, a garbled token, and a token with a broken signature all answer 401."""
        write = self.write_operation(controller, resolver)
        write_path = self.record_path_values(write, record) if write.path_params else {}
        targets = [(controller.get_by_id, dict(record.keys), None)]
        if controller.list_op and not controller.list_op.required_query:
            targets.append((controller.list_op, self.list_path_values(controller, record), None))
        targets.append((write, write_path, self.factory.generate(write.body_schema, resolver)))
        problems, sent = [], 0
        for operation, path_values, body in targets:
            for mode in (AUTH_NONE, AUTH_GARBLED, AUTH_TAMPERED):
                sent += 1
                response = self.client.call(operation, path_values, body=body, auth=mode)
                if response.status != 401:
                    problems.append(f"{operation.label} ({mode} token): HTTP {response.status}")
                    if response.ok and body is not None and not write.path_params:
                        self.remove_unexpected(controller, response, path_values)
        result.check(
            "requests without a valid token are 401",
            not problems,
            summarize(problems) or f"{sent} request(s)",
        )

    def audit_checks(self, result: Result) -> None:
        spec_issues, problem_issues, type_issues, client_errors = self.audit.take()
        result.check("responses match the spec", not spec_issues, summarize(spec_issues))
        if client_errors:
            result.check(
                "4xx responses use ProblemDetails",
                not problem_issues,
                summarize(problem_issues) or f"{client_errors} response(s)",
            )
        if type_issues:
            result.notes.append(
                "ProblemDetails bodies not sent as application/problem+json: "
                + summarize(type_issues)
            )

    def document_notes(self) -> list[str]:
        notes = []
        if not self.audit.rate_limit_headers:
            notes.append(
                f"no rate-limit headers (RateLimit-*, X-RateLimit-*) seen on {self.audit.responses} response(s)"
            )
        missing = self.client.retry_after_missing - self.retry_after_missing_at_start
        if missing:
            notes.append(f"{missing} rate-limited (429) response(s) had no Retry-After header")
        zero = self.client.retry_after_zero - self.retry_after_zero_at_start
        if zero:
            notes.append(f"{zero} rate-limited (429) response(s) advised Retry-After under 1 second")
        if self.audit.rate_limit_issues:
            notes.append(
                "429 bodies are not ProblemDetails: "
                + summarize(list(dict.fromkeys(self.audit.rate_limit_issues)))
            )
        return notes

    # --- controller tests -------------------------------------------------------------------------

    def test_crud(self, controller: Controller) -> Result:
        result = Result(controller)
        resolver = DependencyResolver(self, controller)
        record = self.write_new(controller, resolver, result)
        if record is None:
            self.note_dependencies(result, resolver)
            return result
        if not record.keys:
            result.check(
                "derive GET-by-ID key from write response",
                False,
                json.dumps(record.data)[:200],
            )
            return result

        response = self.read(record)
        if result.check(
            f"read {controller.get_by_id.label}",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            mismatches = compare_record(
                self.spec, controller.read_schema, record.body, response.body
            )
            result.check(
                "read matches written values", not mismatches, "; ".join(mismatches)
            )
            record.data = response.body
            self.check_timestamps_current(controller, record, result)

        if controller.put and (controller.create or not controller.put.path_params):
            self.update_with_put(controller, record, resolver, result)
        elif controller.put:
            # Parent-keyed PUT: the create was a PUT, a second PUT with new values tests the update.
            self.update_with_put(controller, record, resolver, result)

        if controller.patch:
            self.patch_record(controller, record, resolver, result)

        if controller.list_op:
            self.find_in_list(controller, record, result)
            if not controller.list_op.required_query:
                self.check_filters(controller, record, result)
                self.check_paging(controller, record, resolver, result)

        self.negative_sweep(controller, record, resolver, result)
        self.bad_body_checks(controller, record, resolver, result)
        self.auth_checks(controller, record, resolver, result)

        if controller.delete:
            if self.keep:
                extra = self.write_new(controller, resolver, None)
                if extra:
                    self.delete_record(
                        controller, extra, result, "extra record (--keep)"
                    )
                else:
                    result.check("create extra record to delete (--keep)", False)
                result.notes.append(f"kept record {record.keys}")
            else:
                self.delete_record(controller, record, result, "record")
        else:
            result.notes.append(f"no DELETE endpoint, record {record.keys} left behind")

        self.note_dependencies(result, resolver)
        return result

    def update_with_put(
        self,
        controller: Controller,
        record: Record,
        resolver: DependencyResolver,
        result: Result,
    ):
        operation = controller.put
        before = record.data
        self.wait_for_clock_tick(controller, record)
        body = self.factory.generate(operation.body_schema, resolver)
        lowered_data = (
            {norm(k): v for k, v in record.data.items()}
            if isinstance(record.data, dict)
            else {}
        )
        key_names = {norm(name) for name in record.keys} | {"id"}
        fields = self.spec.fields(operation.body_schema)
        changed = False
        for name in list(body):
            # Keep identity and parent links stable so the PUT updates the same record.
            if norm(name) in key_names and norm(name) in lowered_data:
                body[name] = lowered_data[norm(name)]
            elif name in resolver.used and norm(name) in lowered_data:
                body[name] = lowered_data[norm(name)]
            elif norm(name) in resolver.samples:
                changed = True
            elif norm(name) in lowered_data and body[name] == lowered_data[norm(name)]:
                # Some APIs reject a PUT that changes nothing (World API ItemOnUse answers 500
                # NoChangesToModifyException, 2026-10-23), so make sure plain values differ.
                if self.spec.kind(fields[name]) not in ("object", "array"):
                    body[name] = self.factory.scalar(fields[name], avoid=body[name])
                    changed = changed or body[name] != lowered_data[norm(name)]
            else:
                changed = True
        if not changed:
            result.notes.append(
                f"{operation.label} body has no field besides keys and parent links to change"
            )
        path_values = dict(zip(operation.path_params, record.keys.values()))
        response = self.client.call(operation, path_values, body=body)
        if not result.check(
            f"update {operation.label}",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            return
        response = self.read(record)
        if result.check(
            "read after update", response.ok, "" if response.ok else response.describe()
        ):
            mismatches = compare_record(
                self.spec, controller.read_schema, body, response.body
            )
            result.check(
                "read matches updated values", not mismatches, "; ".join(mismatches)
            )
            self.check_timestamps_on_update(
                controller, before, response.body, result, "update"
            )
            record.data = response.body
            record.body = body
            if self.repeat_put(operation, path_values, body, result):
                response = self.read(record)
                mismatches = (
                    compare_record(self.spec, controller.read_schema, body, response.body)
                    if response.ok
                    else [response.describe()]
                )
                result.check(
                    "read after repeat PUT is unchanged",
                    not mismatches,
                    "; ".join(mismatches),
                )

    def repeat_put(
        self,
        operation: Operation,
        path_values: dict[str, Any],
        body: Any,
        result: Result,
    ) -> bool:
        """Send an identical PUT again. PUT is idempotent, so this must succeed (2xx) or answer 304.

        World API (2026-10-23) answered 500 NoChangesToModifyException on most PUTs whose body
        matched the stored record.
        """
        response = self.client.call(operation, path_values, body=body)
        ok = response.ok or response.status == 304
        result.check(
            f"repeat identical {operation.label}",
            ok,
            f"HTTP {response.status}" if ok else response.describe(),
        )
        return ok

    def patch_record(
        self,
        controller: Controller,
        record: Record,
        resolver: DependencyResolver,
        result: Result,
    ):
        operation = controller.patch
        read_fields = {norm(name) for name in self.spec.fields(controller.read_schema)}
        key_names = {norm(name) for name in record.keys} | {"id"}
        current = (
            {norm(k): v for k, v in record.data.items()}
            if isinstance(record.data, dict)
            else {}
        )
        choices = []
        for name, schema in self.spec.fields(operation.body_schema).items():
            kind = self.spec.kind(schema)
            if norm(name) not in read_fields or norm(name) in key_names:
                continue
            if kind not in ("string", "integer", "number", "enum", "boolean", "guid"):
                continue
            if resolver.target(name)[0] is not None:
                continue
            choices.append(({"string": 0, "guid": 2}.get(kind, 1), name, schema))
        if not choices:
            result.notes.append(
                f"{operation.label} only has key or parent-link fields shared with the read model, PATCH not tested"
            )
            return
        choices.sort(key=lambda item: item[0])
        _, name, schema = choices[0]
        has_sample, value = resolver.sample(name)
        if not has_sample:
            value = self.factory.scalar(schema, avoid=current.get(norm(name)))
        body = {name: value}
        before = record.data
        self.wait_for_clock_tick(controller, record)
        path_values = self.record_path_values(operation, record)
        response = self.client.call(operation, path_values, body=body)
        if not result.check(
            f"patch {operation.label} ({name})",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            return
        if response.body is not None:
            mismatches = compare_record(
                self.spec, controller.read_schema, body, response.body
            )
            result.check(
                "patch response reflects change", not mismatches, "; ".join(mismatches)
            )
        response = self.read(record)
        if result.check(
            "read after patch", response.ok, "" if response.ok else response.describe()
        ):
            mismatches = compare_record(
                self.spec, controller.read_schema, body, response.body
            )
            result.check("read reflects patch", not mismatches, "; ".join(mismatches))
            self.check_patch_integrity(controller, name, before, response.body, result)
            self.check_timestamps_on_update(
                controller, before, response.body, result, "patch"
            )
            record.data = response.body
            record.patch_body = body

    def delete_record(
        self, controller: Controller, record: Record, result: Result, what: str
    ) -> None:
        response = self.client.call(controller.delete, self.delete_path(record))
        if not result.check(
            f"delete {what} {controller.delete.label}",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            return
        response = self.read(record)
        gone = response.status in (404, 410)
        result.check(
            "read after delete is 404/410",
            gone,
            "" if gone else f"got HTTP {response.status}",
        )
        response = self.client.call(controller.delete, self.delete_path(record))
        result.check(
            "second DELETE of the same record is 404",
            response.status == 404,
            "" if response.status == 404 else f"got HTTP {response.status}",
        )
        if controller.list_op and not controller.list_op.required_query:
            filters = self.list_key_filters(controller, record)
            found, pages, problem = self.scan_list(controller, record, filters)
            how = f"filtered by {sorted(filters)}" if filters else "unfiltered"
            result.check(
                "deleted record is gone from list",
                not found and not problem,
                problem or ("still listed" if found else f"{how}, {pages} page(s)"),
            )

    def collection_unknown_parent(self, controller: Controller, result: Result) -> None:
        """GET for an unknown parent: 404, or 200 with an empty collection (both acceptable)."""
        _, response = self.unknown_keys(controller)
        items = lowered(response.body).get(norm(controller.collection_read_field))
        empty = response.ok and not items
        result.check(
            f"GET unknown key {controller.get_by_id.label} is 404 or empty",
            response.status == 404 or empty,
            "" if response.status == 404 or empty else response.describe(),
        )

    def test_collection(self, controller: Controller) -> Result:
        """Write two collection items, read them back, then clear the collection."""
        result = Result(controller)
        resolver = DependencyResolver(self, controller)
        operation = controller.put
        path_values = self.parent_path_values(controller, operation, resolver, result)
        if path_values is None:
            result.check("resolve parent record", False, "no parent for the path key")
            self.note_dependencies(result, resolver)
            return result
        keys = dict(zip(controller.get_by_id.path_params, path_values.values()))
        body = self.factory.generate(operation.body_schema, resolver, array_count=2)
        response = self.client.call(operation, path_values, body=body)
        if not result.check(
            f"save {operation.label}",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            self.note_dependencies(result, resolver)
            return result
        response = self.client.call(controller.get_by_id, keys)
        if result.check(
            f"read {controller.get_by_id.label}",
            response.ok,
            "" if response.ok else response.describe(),
        ):
            written = body.get(controller.collection_write_field) or []
            lowered = (
                {norm(k): v for k, v in response.body.items()}
                if isinstance(response.body, dict)
                else {}
            )
            read_items = lowered.get(norm(controller.collection_read_field)) or []
            item_schema = self.spec.array_item(
                self.spec.fields(controller.read_schema)[
                    controller.collection_read_field
                ]
            )
            missing = []
            for item in written:
                if not any(
                    not compare_record(self.spec, item_schema, item, candidate)
                    for candidate in read_items
                ):
                    missing.append(json.dumps(item))
            result.check(
                f"read {controller.collection_read_field} contains written {controller.collection_write_field}",
                not missing and len(read_items) >= len(written),
                f"missing {missing}" if missing else f"{len(read_items)} item(s)",
            )
        self.repeat_put(operation, path_values, body, result)
        record = Record(controller, keys, body, None)
        self.collection_unknown_parent(controller, result)
        self.bad_body_checks(controller, record, resolver, result)
        self.auth_checks(controller, record, resolver, result)
        if not self.keep:
            body = {controller.collection_write_field: []}
            response = self.client.call(operation, path_values, body=body)
            if result.check(
                f"clear {operation.label}",
                response.ok,
                "" if response.ok else response.describe(),
            ):
                response = self.client.call(controller.get_by_id, keys)
                lowered = (
                    {norm(k): v for k, v in response.body.items()}
                    if isinstance(response.body, dict)
                    else {}
                )
                remaining = lowered.get(norm(controller.collection_read_field))
                result.check(
                    "read after clear is empty",
                    response.ok and not remaining,
                    (
                        response.describe()
                        if not response.ok
                        else f"{len(remaining or [])} item(s)"
                    ),
                )
        else:
            result.notes.append(f"kept collection for {keys}")
        self.note_dependencies(result, resolver)
        return result

    def note_dependencies(self, result: Result, resolver: DependencyResolver) -> None:
        for name, target in resolver.used.items():
            result.notes.append(f"{name} -> {target}")
        for missing in resolver.missing:
            result.notes.append(f"dependency unavailable, random value used: {missing}")

    def run(self, only: set[str]) -> list[Result]:
        results = []
        for controller in self.controllers:
            if only and norm(controller.name) not in only:
                continue
            if controller.shape not in (SHAPE_CRUD, SHAPE_COLLECTION):
                result = Result(controller, status=UNTESTED_STATUS[controller.shape])
                result.notes.append(controller.reason)
                results.append(result)
                continue
            info(
                f"\n  {colour(controller.name)} [{controller.shape}: {controller.variant()}]"
            )
            before = set(self.client.endpoints_called)
            self.audit.take()
            if controller.shape == SHAPE_CRUD:
                result = self.test_crud(controller)
            else:
                result = self.test_collection(controller)
            self.audit_checks(result)
            result.endpoints = {
                e
                for e in self.client.endpoints_called - before
                if any(
                    op.method.upper() == e[0] and op.path == e[1]
                    for op in controller.operations
                )
            }
            result.notes = controller.notes + result.notes
            result.finish()
            results.append(result)
        self.cleanup_parents()
        return results


# ---------------------------------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------------------------------


# Report status colours. Untested statuses not listed here stay plain.
STATUS_COLOURS = {
    "PASS": "green",
    "FAIL": "red",
    "SKIPPED": "yellow",
    "IGNORED": "blue",
}


def table(
    headers: list[str], rows: list[list[str]], styles: dict[int, Any] | None = None
) -> str:
    """Render an aligned table. styles maps a column index to cell text -> colour name."""
    styles = styles or {}
    widths = [
        max(len(str(row[i])) for row in [headers] + rows) for i in range(len(headers))
    ]

    def cell(index: int, value: str, width: int) -> str:
        # Pad before colouring so escape codes do not disturb alignment.
        padded = str(value).ljust(width)
        name = styles[index](str(value)) if index in styles else None
        return colour(padded, name) if name else padded

    lines = [
        "| "
        + " | ".join(colour(str(h).ljust(w)) for h, w in zip(headers, widths))
        + " |"
    ]
    lines.append("| " + " | ".join("-" * w for w in widths) + " |")
    for row in rows:
        lines.append(
            "| "
            + " | ".join(cell(i, c, w) for i, (c, w) in enumerate(zip(row, widths)))
            + " |"
        )
    return "\n".join(lines)


def report(
    document_label: str,
    results: list[Result],
    endpoints_tested: int,
    formats: dict[str, int],
    notes: list[str] | None = None,
) -> None:
    info(f"\n{colour('==== Report:')} {fpath(document_label)}")
    rows = []
    for result in results:
        controller = result.controller
        shape = controller.shape
        if controller.shape in (SHAPE_CRUD, SHAPE_COLLECTION):
            shape = f"{controller.shape}: {controller.variant()}"
        elif controller.shape == SHAPE_SKIPPED:
            # File and binary payloads are skipped because they need bespoke test logic.
            shape = f"{controller.shape} ({controller.summary}, suggests special handling)"
        elif controller.summary:
            shape = f"{controller.shape} ({controller.summary})"
        passed = sum(1 for _, ok, _ in result.checks if ok)
        checks = f"{passed}/{len(result.checks)}" if result.checks else "-"
        endpoints = str(len(result.endpoints)) if result.checks else "-"
        rows.append([controller.name, shape, result.status, checks, endpoints])
    info(
        table(
            ["Controller", "Shape", "Status", "Checks", "Endpoints"],
            rows,
            {2: STATUS_COLOURS.get},
        )
    )
    noted = [r for r in results if r.notes or any(not ok for _, ok, _ in r.checks)]
    if noted:
        info(f"\n{colour('Notes:')}")
        for result in noted:
            for label, ok, detail in result.checks:
                if not ok:
                    info(
                        f"* {result.controller.name}: {colour('FAILED', 'red')} {label}"
                        + (f": {detail}" if detail else "")
                    )
            for note in dict.fromkeys(result.notes):
                if note:
                    info(f"* {result.controller.name}: {note}")
    info(f"\n{colour('Endpoints tested:')} {endpoints_tested}")
    if formats:
        info(
            "String/number formats generated: "
            + ", ".join(f"{k} ({v})" for k, v in sorted(formats.items()))
        )
    for note in notes or []:
        info(f"{colour('Note:', 'yellow')} {note}")


# ---------------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------------


def find_swagger_documents(swagger_dir: Path) -> list[Path]:
    found = []
    for path in sorted(swagger_dir.rglob("swagger.json")):
        if {"bin", "obj"} & set(path.relative_to(swagger_dir).parts):
            continue
        found.append(path)
    return found


def build_controllers(spec: Spec, ignored: set[str]) -> list[Controller]:
    grouped: dict[str, list[Operation]] = {}
    for operation in load_operations(spec):
        grouped.setdefault(operation.tag, []).append(operation)
    controllers = [
        Controller(name, operations) for name, operations in sorted(grouped.items())
    ]
    for controller in controllers:
        classify(spec, controller, ignored)
    return controllers


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Integration-test the basic RESTful CRUD controllers of the running local API container.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Path defaults:\n"
            f"  config dir   ${ENV_CONFIG_DIR}, else {DEFAULT_CONFIG_DIR}\n"
            f"  config file  ${ENV_CONFIG_FILE}, else <config dir>/{CONFIG_FILE_NAME} if present\n"
            f"  compose file ${ENV_COMPOSE_FILE}, else first of {', '.join(COMPOSE_FILE_NAMES)} in <config dir>\n"
            f"  swagger dir  ${ENV_SWAGGER_DIR}, else <config dir>/../../swagger\n"
            "An explicitly named config or Compose file must exist."
        ),
    )
    parser.add_argument(
        "--config-dir",
        help="Directory holding the config and Compose files (default: test/local)",
    )
    parser.add_argument(
        "--config", help=f"Config YAML file (default: <config dir>/{CONFIG_FILE_NAME})"
    )
    parser.add_argument("--compose-file", help="Docker Compose file to read ports from")
    parser.add_argument(
        "--swagger-dir", help="Directory searched for swagger.json documents"
    )
    parser.add_argument(
        "--keep",
        action="store_true",
        help="Keep created records (still deletes one extra per DELETE endpoint)",
    )
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        metavar="CONTROLLER",
        help="Only test this controller (repeatable)",
    )
    parser.add_argument(
        "--classify-only",
        action="store_true",
        help="Print controller shapes without calling the API",
    )
    parser.add_argument(
        "-v", "--verbose", action="store_true", help="Print every request and response"
    )
    parser.add_argument(
        "--no-color",
        action="store_true",
        help=(
            "Disable coloured output (also disabled by a non-empty "
            f"${ENV_NO_COLOR}, or when output is not a terminal)"
        ),
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    global VERBOSE
    args = parse_args(argv)
    VERBOSE = args.verbose
    enable_colours(not args.no_color and not os.environ.get(ENV_NO_COLOR))
    try:
        return run(args)
    except SetupError as exc:
        error(str(exc))
        return 2
    except PortClosedError as exc:
        error(str(exc))
        error(
            "Make sure the supporting services are running (test/local/scripts/start-supporting-services.sh) "
            "and that the API is up (docker compose up -d api)."
        )
        return 3


def run(args: argparse.Namespace) -> int:
    settings = resolve_settings(args)
    config = settings.config
    ignored = {norm(name) for name in config.get("ignore") or []}
    only = {norm(name) for name in args.only}

    documents = find_swagger_documents(settings.swagger_dir)
    if not documents:
        raise SetupError(
            f"No swagger.json documents found under {fpath(settings.swagger_dir, sys.stderr)}"
        )

    info(f"{colour('Config dir:')}   {fpath(settings.config_dir)}")
    info(
        f"{colour('Config file:')}  "
        + (fpath(settings.config_file) if settings.config_file else "(none)")
    )
    info(f"{colour('Compose file:')} {fpath(settings.compose_file)}")

    client = None
    if not args.classify_only:
        compose = load_compose(settings.compose_file)
        api_name, api_service = find_service(
            compose, config.get("api_service") or DEFAULT_API_SERVICE
        )
        kc_name, kc_service = find_service(
            compose,
            config.get("keycloak_service") or DEFAULT_KEYCLOAK_SERVICE,
            image_hint="keycloak",
        )
        api_port = published_port(api_name, api_service)
        kc_port = published_port(kc_name, kc_service)
        check_port(kc_port, f"Keycloak ({kc_name})")
        check_port(api_port, f"API ({api_name})")
        auth = resolve_auth(
            config, kc_port, keycloak_realm_files(settings.compose_file, kc_service)
        )
        info(f"{colour('API:')}          http://localhost:{api_port}")
        info(
            f"{colour('Token:')}        {auth.token_url} (client {auth.client_id!r}, user {auth.username!r})"
        )
        tokens = TokenProvider(auth)
        tokens.get()
        client = ApiClient(f"http://localhost:{api_port}", tokens)

    exit_code = 0
    for document_path in documents:
        label = str(document_path.relative_to(settings.swagger_dir.parent))
        info(f"\n{colour('====')} {fpath(label)}")
        try:
            document = json.loads(document_path.read_text())
        except (OSError, json.JSONDecodeError) as exc:
            error(f"Unable to read {fpath(document_path, sys.stderr)}: {exc}")
            exit_code = max(exit_code, 2)
            continue
        if not str(document.get("openapi", "")).startswith("3"):
            warn(f"{fpath(label, sys.stderr)} is not an OpenAPI v3 document, skipped")
            continue
        spec = Spec(document)
        controllers = build_controllers(spec, ignored)
        for name in sorted(ignored - {norm(c.name) for c in controllers}):
            warn(
                f"config ignores {name!r}, which is not a controller in {fpath(label, sys.stderr)}"
            )

        considered = [c for c in controllers if c.shape != SHAPE_IGNORED]
        testable = [c for c in considered if c.shape in (SHAPE_CRUD, SHAPE_COLLECTION)]

        if args.classify_only:
            results = []
            for controller in controllers:
                result = Result(
                    controller, status=UNTESTED_STATUS.get(controller.shape, "not run")
                )
                result.notes = controller.notes + (
                    [controller.reason] if controller.reason else []
                )
                results.append(result)
            report(label, results, 0, {})
        else:
            runner = Runner(spec, client, config, controllers, args.keep)
            before = set(client.endpoints_called)
            results = runner.run(only)
            report(
                label,
                results,
                len(client.endpoints_called - before),
                runner.factory.formats_seen,
                runner.document_notes(),
            )
            if any(r.status == STATUS_FAIL for r in results):
                exit_code = max(exit_code, 1)

        if not considered:
            error(
                f"{fpath(label, sys.stderr)}: no controllers to consider (all ignored or none found)"
            )
            exit_code = max(exit_code, 2)
        elif not testable:
            warn(f"{fpath(label, sys.stderr)}: no basic CRUD controllers detected")
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
