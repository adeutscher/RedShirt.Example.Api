#!/bin/bash

awslocal dynamodb create-table --table-name example --attribute-definitions AttributeName=Name,AttributeType=S --key-schema AttributeName=Name,KeyType=HASH --provisioned-throughput ReadCapacityUnits=10,WriteCapacityUnits=10
