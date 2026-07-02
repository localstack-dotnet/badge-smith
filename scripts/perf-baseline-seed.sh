#!/usr/bin/env bash
set -euo pipefail

NET="${1:-bs-perf-net}"
export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1 AWS_REGION=us-east-1

LS_PORT="$(docker port bs-perf-ls 4566/tcp 2>/dev/null | head -1 | sed 's/.*://')"
if [[ -n "$LS_PORT" ]] && command -v aws >/dev/null 2>&1; then
  AWS=(aws --endpoint-url "http://localhost:$LS_PORT")
else
  AWS=(docker run --rm --network "$NET" \
    -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_DEFAULT_REGION=us-east-1 -e AWS_REGION=us-east-1 \
    amazon/aws-cli:2.17.62 --endpoint-url http://localstack:4566)
fi

table_exists() {
  "${AWS[@]}" dynamodb describe-table --table-name "$1" >/dev/null 2>&1
}

create_pk_sk_table() {
  local table="$1"
  if table_exists "$table"; then
    return
  fi

  "${AWS[@]}" dynamodb create-table --table-name "$table" \
    --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S \
    --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --billing-mode PAY_PER_REQUEST >/dev/null
  "${AWS[@]}" dynamodb wait table-exists --table-name "$table"
}

create_test_result_table() {
  if table_exists badge-smith-test-result; then
    return
  fi

  "${AWS[@]}" dynamodb create-table --table-name badge-smith-test-result \
    --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S \
    --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE \
    --global-secondary-indexes 'IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL}' \
    --billing-mode PAY_PER_REQUEST >/dev/null
  "${AWS[@]}" dynamodb wait table-exists --table-name badge-smith-test-result
}

create_secret() {
  local name="$1"
  local value="$2"
  if "${AWS[@]}" secretsmanager describe-secret --secret-id "$name" >/dev/null 2>&1; then
    return
  fi

  "${AWS[@]}" secretsmanager create-secret --name "$name" --secret-string "$value" >/dev/null
}

create_pk_sk_table badge-smith-hmac-nonce
create_pk_sk_table badge-smith-github-org-secrets
create_test_result_table

create_secret badgesmith/github/test-org/testdata contract-test-secret
create_secret badgesmith/github/test-org/package dummy-github-pat
create_secret badgesmith/github/localstack-dotnet/package dummy-github-pat

"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#testdata"},"SecretName":{"S":"badgesmith/github/test-org/testdata"}}' >/dev/null
"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#package"},"SecretName":{"S":"badgesmith/github/test-org/package"}}' >/dev/null
"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#localstack-dotnet"},"SK":{"S":"CONST#GITHUB#package"},"SecretName":{"S":"badgesmith/github/localstack-dotnet/package"}}' >/dev/null
