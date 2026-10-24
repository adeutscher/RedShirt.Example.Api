#!/bin/bash

# Creates local MiniStack resources. Safe to re-run.
# Runs automatically via the ministack-init Compose service, or manually from the host.

set -e

export AWS_DEFAULT_REGION=us-east-1

if ! awslocal dynamodb describe-table --table-name example > /dev/null 2>&1; then
    awslocal dynamodb create-table \
        --table-name example \
        --attribute-definitions AttributeName=Name,AttributeType=S \
        --key-schema AttributeName=Name,KeyType=HASH \
        --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10
fi

awslocal ssm put-parameter --overwrite --type String --name /redis/connection-string --value "redis:6379"

awslocal ssm put-parameter --overwrite --type String \
    --name /mysql/connection-string \
    --value "Server=mariadb;Port=3306;Database=example;Uid=example-user;Pwd=example-password;"

awslocal ssm put-parameter --overwrite --type String \
    --name /foo/api-key \
    --value "local-foo-api-key"

awslocal ssm put-parameter --overwrite --type String \
    --name /bar/oauth/client-id \
    --value "local-bar-client-id"

awslocal ssm put-parameter --overwrite --type String \
    --name /bar/oauth/client-secret \
    --value "local-bar-client-secret"

for bucket in unverified-uploads verified-uploads; do
    if ! awslocal s3api head-bucket --bucket "${bucket}" > /dev/null 2>&1; then
        awslocal s3 mb "s3://${bucket}"
    fi
done
