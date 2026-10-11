#!/usr/bin/env bash
#
# Build stage: restore, build, test, and verify committed swagger.json is current.
#
# NSwag runs on PostBuild. If the OpenAPI document drifted from source, swagger.json
# changes on disk and this script fails.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

sln="${SLN_FILE:-RedShirt.Example.Api.slnx}"
configuration="${BUILD_CONFIGURATION:-Release}"
swagger_rel="swagger/RedShirt.Example.Api.Interop/swagger.json"

swagger_sha256() {
  local path="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$path" | awk '{print $1}'
  else
    shasum -a 256 "$path" | awk '{print $1}'
  fi
}

if [[ ! -f "$swagger_rel" ]]; then
  echo "ERROR: $swagger_rel not found." >&2
  exit 1
fi

echo "==> dotnet restore $sln"
dotnet restore "$sln"

swagger_before="$(swagger_sha256 "$swagger_rel")"
echo "==> swagger.json sha256 before build: $swagger_before"

echo "==> dotnet build $sln (--no-restore, -c $configuration)"
dotnet build "$sln" --no-restore --configuration "$configuration"

swagger_after="$(swagger_sha256 "$swagger_rel")"
echo "==> swagger.json sha256 after build:  $swagger_after"

if [[ "$swagger_before" != "$swagger_after" ]]; then
  echo "ERROR: build changed $swagger_rel." >&2
  echo "Regenerate and commit the OpenAPI document (NSwag PostBuild on RedShirt.Example.Api)." >&2
  exit 1
fi

echo "==> dotnet test $sln (--no-build, -c $configuration)"
dotnet test "$sln" --no-build --configuration "$configuration"

echo "==> build clean"
