#!/bin/bash
#
# Generates the TLS certificate for a local Azure Key Vault emulator. Only needed if this template is pivoted from
# SSM to Azure Key Vault as its secret manager. See "Pivoting to Azure Key Vault" in test/local/README.md.
#
# Writes, relative to test/local/:
#   * config/azure-key-vault/emulator.local.{key,crt,pfx}: the emulator's key material. Mount into the emulator only.
#   * config/azure-key-vault-ca/emulator.local.crt: the public certificate alone. Mount into the api container and
#     point COMMON__SECRETS__AZURE_KEY_VAULT__LOCAL_TESTING_CA_CERTIFICATE_PATH at it.
#
# Both directories are git-ignored for these files. Requires openssl.
#
# Ported from the JobWorker template.
set -euo pipefail

# Work from test/local/, two levels above this script.
cd "$(dirname "$(readlink -f "$0")")/../.." || exit 1

mkdir -p config/azure-key-vault config/azure-key-vault-ca

# Generate private key and self-signed certificate for the Azure Key Vault emulator.
# The SAN must cover every name that clients use to reach the emulator:
#   * azure-key-vault-emulator.vault.azure.net: the api container, inside the compose network
#   * localhost and 127.0.0.1: helper scripts on the host
openssl req -x509 -newkey rsa:4096 -sha256 -days 365 -nodes \
  -keyout config/azure-key-vault/emulator.local.key -out config/azure-key-vault/emulator.local.crt \
  -subj "/CN=azure-key-vault-emulator" \
  -addext "subjectAltName=DNS:azure-key-vault-emulator.vault.azure.net,DNS:azure-keyvault,DNS:localhost,IP:127.0.0.1"

# Package into PFX format for ASP.NET Core Kestrel (the emulator). Local-only password.
openssl pkcs12 -export -out config/azure-key-vault/emulator.local.pfx \
  -inkey config/azure-key-vault/emulator.local.key -in config/azure-key-vault/emulator.local.crt \
  -password pass:YourSecurePassword

# The emulator container reads the PFX as a different user, so it must be readable by others.
# The PEM private key is not used by any container, so it stays private to you.
chmod 600 config/azure-key-vault/emulator.local.key
chmod o+r config/azure-key-vault/emulator.local.pfx config/azure-key-vault/emulator.local.crt

# Publish only the public certificate for clients to pin. Mount this directory into the api container,
# so the API never sees the private key or PFX.
cp config/azure-key-vault/emulator.local.crt config/azure-key-vault-ca/emulator.local.crt
chmod 644 config/azure-key-vault-ca/emulator.local.crt

echo "Wrote config/azure-key-vault/ (emulator key material) and config/azure-key-vault-ca/emulator.local.crt (public)."
