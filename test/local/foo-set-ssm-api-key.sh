#!/bin/bash
set -euo pipefail

# Set the Foo API key in ministack SSM (/foo/api-key). Does not update WireMock stubs.
# Default value is intentionally invalid against the default WireMock mappings (local-foo-api-key).
# To rotate a key WireMock will also accept: ./foo-rotate-api-key.sh [new-key]
#
# Usage:
#   ./foo-set-ssm-api-key.sh [key]
#
# Examples:
#   ./foo-set-ssm-api-key.sh
#   ./foo-set-ssm-api-key.sh 'bogus-key-value-here'

KEY="${1:-bad-foo-api-key}"

echo "Setting SSM /foo/api-key → ${KEY}"
AWS_DEFAULT_REGION=us-east-1 awslocal ssm put-parameter --overwrite --type String \
    --name /foo/api-key \
    --value "${KEY}"
