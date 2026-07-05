#!/usr/bin/env bash
set -euo pipefail

NET="${1:-bs-perf-net}"
export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1 AWS_REGION=us-east-1
GITHUB_PACKAGE_TOKEN="${GITHUB_TOKEN:-dummy-github-pat}"

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

put_test_result() {
  local owner="$1"
  local repo="$2"
  local platform="$3"
  local branch="$4"
  local run_id="$5"
  local timestamp="$6"
  local url_html="https://github.com/$owner/$repo/actions/runs/$run_id"
  local workflow_run_url="https://api.github.com/repos/$owner/$repo/actions/runs/$run_id"

  "${AWS[@]}" dynamodb put-item --table-name badge-smith-test-result \
    --item "{\"PK\":{\"S\":\"TEST#$owner#$repo\"},\"SK\":{\"S\":\"RESULT#$platform#$branch#$timestamp\"},\"GSI1PK\":{\"S\":\"LATEST#$owner#$repo#$platform#$branch\"},\"GSI1SK\":{\"S\":\"$timestamp\"},\"Owner\":{\"S\":\"$owner\"},\"Repo\":{\"S\":\"$repo\"},\"Platform\":{\"S\":\"$platform\"},\"Branch\":{\"S\":\"$branch\"},\"Passed\":{\"N\":\"42\"},\"Failed\":{\"N\":\"0\"},\"Skipped\":{\"N\":\"1\"},\"Total\":{\"N\":\"43\"},\"Timestamp\":{\"S\":\"$timestamp\"},\"Commit\":{\"S\":\"perfseed\"},\"RunId\":{\"S\":\"$run_id\"},\"UrlHtml\":{\"S\":\"$url_html\"},\"WorkflowRunUrl\":{\"S\":\"$workflow_run_url\"},\"CreatedAt\":{\"S\":\"$timestamp\"},\"TTL\":{\"N\":\"1924992000\"}}" >/dev/null
}

create_pk_sk_table badge-smith-hmac-nonce
create_pk_sk_table badge-smith-github-org-secrets
create_test_result_table

create_secret badgesmith/github/test-org/testdata contract-test-secret
create_secret badgesmith/github/test-org/package "$GITHUB_PACKAGE_TOKEN"
create_secret badgesmith/github/localstack-dotnet/package "$GITHUB_PACKAGE_TOKEN"

"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#testdata"},"SecretName":{"S":"badgesmith/github/test-org/testdata"}}' >/dev/null
"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#test-org"},"SK":{"S":"CONST#GITHUB#package"},"SecretName":{"S":"badgesmith/github/test-org/package"}}' >/dev/null
"${AWS[@]}" dynamodb put-item --table-name badge-smith-github-org-secrets \
  --item '{"PK":{"S":"ORG#localstack-dotnet"},"SK":{"S":"CONST#GITHUB#package"},"SecretName":{"S":"badgesmith/github/localstack-dotnet/package"}}' >/dev/null

put_test_result localstack-dotnet localstack.client linux main 1001 2026-01-01T00:00:01.000Z
put_test_result microsoft vscode windows main 1002 2026-01-01T00:00:02.000Z
put_test_result facebook react linux main 1003 2026-01-01T00:00:03.000Z
put_test_result dotnet aspnetcore linux release/8.0 1004 2026-01-01T00:00:04.000Z
put_test_result AutoMapper AutoMapper windows master 1005 2026-01-01T00:00:05.000Z
