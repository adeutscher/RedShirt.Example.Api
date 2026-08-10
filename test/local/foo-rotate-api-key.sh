#!/bin/bash
set -euo pipefail

# Rotate the Foo API key in ministack SSM and update WireMock's in-memory stubs to accept it.
# Does not rewrite files under wiremock/foo/mappings/ — a WireMock restart restores those defaults.
#
# Usage:
#   ./rotate-foo-api-key.sh [new-key]
#
# Examples:
#   ./rotate-foo-api-key.sh
#   ./rotate-foo-api-key.sh my-new-foo-key
#
# Pair with ./foo-set-ssm-api-key.sh (SSM only → 401) then this script (SSM + WireMock → recovery).

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WIREMOCK_URL="${WIREMOCK_URL:-http://localhost:9100}"
SSM_PARAM_NAME="${FOO_API_KEY_SSM_PATH:-/foo/api-key}"
NEW_KEY="${1:-rotated-foo-api-key-$(date +%s)}"

if ! command -v jq >/dev/null 2>&1; then
    echo "jq is required to update WireMock stubs via the Admin API." >&2
    exit 1
fi

if ! command -v awslocal >/dev/null 2>&1; then
    echo "awslocal is required to update the SSM parameter." >&2
    exit 1
fi

if ! curl -sf "${WIREMOCK_URL}/__admin/health" >/dev/null \
    && ! curl -sf "${WIREMOCK_URL}/__admin/mappings" >/dev/null; then
    echo "WireMock Admin API is not reachable at ${WIREMOCK_URL}." >&2
    echo "Start it with: (cd \"${ROOT_DIR}\" && docker compose up -d wiremock-foo)" >&2
    exit 1
fi

echo "Setting SSM ${SSM_PARAM_NAME} → ${NEW_KEY}"
AWS_DEFAULT_REGION=us-east-1 awslocal ssm put-parameter --overwrite --type String \
    --name "${SSM_PARAM_NAME}" \
    --value "${NEW_KEY}" >/dev/null

echo "Updating WireMock stubs at ${WIREMOCK_URL} (in-memory only)"
updated_count=0
while IFS= read -r stub; do
    id="$(jq -r '.id' <<<"${stub}")"
    has_api_key_matcher="$(jq -r '.request.headers["x-api-key"].equalTo // empty' <<<"${stub}")"
    if [[ -z "${has_api_key_matcher}" ]]; then
        continue
    fi

    updated_stub="$(jq --arg key "${NEW_KEY}" \
        '.request.headers["x-api-key"].equalTo = $key' <<<"${stub}")"

    curl -sf -X PUT \
        -H 'Content-Type: application/json' \
        -d "${updated_stub}" \
        "${WIREMOCK_URL}/__admin/mappings/${id}" >/dev/null

    updated_count=$((updated_count + 1))
    echo "  updated stub ${id} (${has_api_key_matcher} → ${NEW_KEY})"
done < <(curl -sf "${WIREMOCK_URL}/__admin/mappings" | jq -c '.mappings[]')

if [[ "${updated_count}" -eq 0 ]]; then
    echo "No WireMock stubs with an x-api-key equalTo matcher were found." >&2
    exit 1
fi

echo "Done. Rotated ${updated_count} stub(s). New key: ${NEW_KEY}"
