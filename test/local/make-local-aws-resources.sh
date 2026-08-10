#!/bin/bash

AWS_DEFAULT_REGION=us-east-1 awslocal dynamodb create-table \
    --table-name example \
    --attribute-definitions AttributeName=Name,AttributeType=S \
    --key-schema AttributeName=Name,KeyType=HASH \
    --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10

AWS_DEFAULT_REGION=us-east-1 awslocal ssm put-parameter --overwrite --type String --name /redis/connection-string --value "redis:6379"

AWS_DEFAULT_REGION=us-east-1 awslocal ssm put-parameter --overwrite --type String \
    --name /mysql/connection-string \
    --value "Server=mariadb;Port=3306;Database=example;Uid=example-user;Pwd=example-password;"

AWS_DEFAULT_REGION=us-east-1 awslocal ssm put-parameter --overwrite --type String \
    --name /foo/api-key \
    --value "local-foo-api-key"
