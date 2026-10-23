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

    def call(
        self,
        operation: "Operation",
        path_values: dict[str, Any] | None = None,
        query: dict[str, Any] | None = None,
        body: Any = None,
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
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        self.endpoints_called.add((operation.method.upper(), operation.path))
        return self._send(operation.method.upper(), url, headers, data)

    def _send(
        self, method: str, url: str, headers: dict[str, str], data: bytes | None
    ) -> Response:
        backoff = 1.0
        refreshed = False
        for attempt in range(1, MAX_RETRY_ATTEMPTS + 1):
            headers["Authorization"] = f"Bearer {self.tokens.get()}"
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
            except urllib.error.HTTPError as exc:
                status = exc.code
                text = exc.read().decode("utf-8", "replace")
                retry_after = exc.headers.get("Retry-After")
            except urllib.error.URLError as exc:
                return Response(0, None, f"connection failed: {exc.reason}")
            if status == 401 and not refreshed:
                self.tokens.get(force=True)
                refreshed = True
                continue
            if status == 429 and attempt < MAX_RETRY_ATTEMPTS:
                delay = retry_after_seconds(retry_after)
                if delay is None:
                    delay = backoff
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
            return Response(status, body, text)
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

    for op in controller.operations:
        if (
            op.method == "patch"
            and op.body_schema
            and len(op.path_params) == key_count
            and controller.patch is None
        ):
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
        path_values = {}
        if operation.path_params:
            # List scoped to a parent (World API ItemEquipmentStat_ListByItemId style); reuse key values.
            path_values = dict(zip(operation.path_params, record.keys.values()))
        key_items = {norm(k): v for k, v in record.keys.items()}
        record_fields = (
            {norm(k): v for k, v in record.data.items()}
            if isinstance(record.data, dict)
            else {}
        )
        # Narrow the search with query parameters named after a key parameter, or after a record field
        # holding a key value (Customer GET /{id} with search ?Id=, Item GET /{entryId} with ?Id=).
        key_values = {str(v).lower() for v in record.keys.values()}
        key_fields = {
            name
            for name, value in record_fields.items()
            if str(value).lower() in key_values
        }
        filters = {}
        for name in operation.query_params:
            lowered = norm(name)
            if lowered in key_items:
                filters[name] = key_items[lowered]
            elif lowered in key_fields:
                filters[name] = record_fields[lowered]
        pagination = pagination_for(self.spec, operation)

        def scan(query: dict) -> tuple[bool, int, str]:
            pages = 0
            for response in pagination.pages(
                self.client, operation, path_values, query
            ):
                pages += 1
                if not response.ok or not isinstance(response.body, dict):
                    return False, pages, response.describe()
                lowered_body = {norm(k): v for k, v in response.body.items()}
                for item in lowered_body.get(norm(controller.list_array_field)) or []:
                    if self.matches_keys(item, record):
                        return True, pages, ""
            return False, pages, ""

        found, pages, problem = scan(filters) if filters else (False, 0, "")
        how = f"filtered by {sorted(filters)}"
        if not found and not problem:
            found, pages, problem = scan({})
            how = "unfiltered"
        detail = problem or f"{how}, {pages} page(s)"
        result.check(f"list {operation.label} contains record", found, detail)

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

        if controller.put and (controller.create or not controller.put.path_params):
            self.update_with_put(controller, record, resolver, result)
        elif controller.put:
            # Parent-keyed PUT: the create was a PUT, a second PUT with new values tests the update.
            self.update_with_put(controller, record, resolver, result)

        if controller.patch:
            self.patch_record(controller, record, resolver, result)

        if controller.list_op:
            self.find_in_list(controller, record, result)

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
        path_values = dict(zip(operation.path_params, record.keys.values()))
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
            record.data = response.body

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
            if controller.shape == SHAPE_CRUD:
                result = self.test_crud(controller)
            else:
                result = self.test_collection(controller)
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
