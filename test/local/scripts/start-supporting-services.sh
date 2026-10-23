#!/usr/bin/env bash
#
# Lazy start button: brings up every test/local Compose service except the API host.
# Run the API on the host (dotnet run) or start the api service separately when you need it.
#
set -euo pipefail

local_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$local_root"

exec docker compose up -d \
  ministack \
  ministack-init \
  redis \
  mariadb \
  wiremock-foo \
  wiremock-bar \
  keycloak
