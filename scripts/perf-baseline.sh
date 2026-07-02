#!/usr/bin/env bash
set -euo pipefail

LABEL="baseline"
UPSTREAM="mock"
ARCH="amd64"
VUS="${K6_VUS:-5}"
DURATION="${K6_DURATION:-60s}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --label) LABEL="$2"; shift 2 ;;
    --upstream) UPSTREAM="$2"; shift 2 ;;
    --arch) ARCH="$2"; shift 2 ;;
    *) echo "unknown arg $1"; exit 1 ;;
  esac
done

if [[ "$UPSTREAM" != "mock" && "$UPSTREAM" != "real" ]]; then
  echo "--upstream must be mock or real" >&2
  exit 1
fi

if [[ "$ARCH" != "amd64" && "$ARCH" != "arm64" ]]; then
  echo "--arch must be amd64 or arm64" >&2
  exit 1
fi

RID="linux-x64"
PLATFORM="linux/amd64"
if [[ "$ARCH" == "arm64" ]]; then
  RID="linux-arm64"
  PLATFORM="linux/arm64"
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOST_ROOT="$ROOT"
if [[ "$ROOT" == /mnt/wsl/docker-desktop-bind-mounts/* ]] && command -v powershell.exe >/dev/null 2>&1; then
  HOST_ROOT="$(powershell.exe -NoProfile -Command '(Get-Location).Path' | tr -d '\r')"
fi
OUT_DIR="$ROOT/docs/research/baselines"
mkdir -p "$OUT_DIR" "$ROOT/artifacts"
STAMP="$(date -u +%Y-%m-%d)"
OUT="$OUT_DIR/$STAMP-$LABEL.json"
IMG="badge-smith:perf-$ARCH"
NET="bs-perf-net"
K6_JSON="$ROOT/artifacts/k6-summary.json"
WIREMOCK_ROOT="$ROOT/tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock"

if command -v python >/dev/null 2>&1; then
  PYTHON=(python)
  PYTHON_NEEDS_WIN_PATHS=false
elif command -v python3 >/dev/null 2>&1; then
  PYTHON=(python3)
  PYTHON_NEEDS_WIN_PATHS=false
elif command -v python.exe >/dev/null 2>&1; then
  PYTHON=(python.exe)
  PYTHON_NEEDS_WIN_PATHS=true
elif command -v py.exe >/dev/null 2>&1; then
  PYTHON=(py.exe -3)
  PYTHON_NEEDS_WIN_PATHS=true
else
  echo "python is required" >&2
  exit 1
fi

if command -v k6 >/dev/null 2>&1; then
  K6=(k6)
  K6_MODE=native
  K6_NEEDS_WIN_PATHS=false
elif command -v k6.exe >/dev/null 2>&1; then
  K6=(k6.exe)
  K6_MODE=windows
  K6_NEEDS_WIN_PATHS=true
else
  echo "k6 is required" >&2
  exit 1
fi

to_host_path() {
  local path="$1"
  if [[ "$HOST_ROOT" != "$ROOT" && "$path" == "$ROOT"/* ]]; then
    local rest
    rest="${path#"$ROOT"/}"
    printf '%s\\%s\n' "$HOST_ROOT" "${rest//\//\\}"
    return
  fi
  case "$path" in
    /mnt/[a-zA-Z]/*)
      local drive rest
      drive="${path:5:1}"
      rest="${path:7}"
      printf '%s:%s\n' "${drive^^}" "\\${rest//\//\\}"
      ;;
    /[a-zA-Z]/*)
      local drive rest
      drive="${path:1:1}"
      rest="${path:3}"
      printf '%s:%s\n' "${drive^^}" "\\${rest//\//\\}"
      ;;
    *)
      echo "$path"
      ;;
  esac
}

cleanup() {
  local exit_code=$?
  docker rm -f bs-perf-lambda bs-perf-wm bs-perf-ls >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  return "$exit_code"
}
trap 'exit_code=$?; cleanup; exit $exit_code' EXIT

mem_to_kb() {
  "${PYTHON[@]}" - "$1" <<'PY'
import re, sys
s = sys.argv[1].strip()
m = re.match(r"([0-9]+(?:\.[0-9]+)?)([KMGTP]?i?B?)", s, re.I)
if not m:
    print(0)
    raise SystemExit
value = float(m.group(1))
unit = m.group(2).lower()
factors = {
    "b": 1 / 1024, "": 1 / 1024,
    "k": 1, "kb": 1, "kib": 1,
    "m": 1024, "mb": 1024, "mib": 1024,
    "g": 1024 * 1024, "gb": 1024 * 1024, "gib": 1024 * 1024,
    "t": 1024 * 1024 * 1024, "tb": 1024 * 1024 * 1024, "tib": 1024 * 1024 * 1024,
}
print(int(value * factors.get(unit, 0)))
PY
}

wait_for_http() {
  local url="$1"
  local timeout_seconds="$2"
  local start
  start="$(date +%s)"
  until curl -fsS "$url" >/dev/null 2>&1; do
    if (( $(date +%s) - start > timeout_seconds )); then
      echo "timed out waiting for $url" >&2
      return 1
    fi
    sleep 1
  done
}

echo "== build image + artifacts =="
docker build --platform "$PLATFORM" -f "$ROOT/src/BadgeSmith.Api/Dockerfile" --build-arg RID="$RID" --target lambda-image -t "$IMG" "$ROOT"
docker build --platform "$PLATFORM" -f "$ROOT/src/BadgeSmith.Api/Dockerfile" --build-arg RID="$RID" --build-arg MSTAT=true --target export-mstat -o "$ROOT/artifacts/mstat" "$ROOT"
docker build --platform "$PLATFORM" -f "$ROOT/src/BadgeSmith.Api/Dockerfile" --build-arg RID="$RID" --target export-zip -o "$ROOT/artifacts" "$ROOT"
ZIP="$ROOT/artifacts/badge-lambda-$RID.zip"
ZIP_BYTES=$(stat -c%s "$ZIP" 2>/dev/null || stat -f%z "$ZIP")
BIN_BYTES=$(unzip -l "$ZIP" | awk '/bootstrap$/ {print $1}')

echo "== boot stack =="
cleanup
docker network create "$NET" >/dev/null
docker run -d --name bs-perf-ls --network "$NET" --network-alias localstack -p 4566 localstack/localstack:4.6 >/dev/null
if [[ "$UPSTREAM" == "mock" ]]; then
  WIREMOCK_ROOT="$ROOT/artifacts/perf-wiremock"
  rm -rf "$WIREMOCK_ROOT"
  mkdir -p "$WIREMOCK_ROOT"
  cp -R "$ROOT/tests/BadgeSmith.Api.Tests/Testing/Infrastructure/wiremock/." "$WIREMOCK_ROOT/"
  cat > "$WIREMOCK_ROOT/mappings/perf-nuget-index-ok.json" <<'JSON'
{
  "request": { "method": "GET", "urlPathPattern": "/nuget/v3-flatcontainer/.*/index\\.json" },
  "response": {
    "status": 200,
    "headers": { "Content-Type": "application/json", "ETag": "\"perf-etag-1\"" },
    "jsonBody": { "versions": [ "1.0.0", "1.2.3", "2.0.0-preview.1" ] }
  }
}
JSON
  cat > "$WIREMOCK_ROOT/mappings/perf-github-versions-ok.json" <<'JSON'
{
  "request": { "method": "GET", "urlPathPattern": "/github/orgs/localstack-dotnet/packages/nuget/.*/versions" },
  "response": {
    "status": 200,
    "headers": { "Content-Type": "application/json" },
    "jsonBody": [ { "id": 1, "name": "1.0.0" }, { "id": 2, "name": "1.2.3" }, { "id": 3, "name": "2.0.0-preview.1" } ]
  }
}
JSON
fi
docker run -d --name bs-perf-wm --network "$NET" --network-alias wiremock \
  -v "$WIREMOCK_ROOT:/home/wiremock:ro" wiremock/wiremock:3.9.1 >/dev/null

LS_PORT=""
for _ in {1..60}; do
  LS_PORT="$(docker port bs-perf-ls 4566/tcp 2>/dev/null | head -1 | sed 's/.*://')"
  if [[ -n "$LS_PORT" ]] && curl -fsS "http://localhost:$LS_PORT/_localstack/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
if [[ -z "$LS_PORT" ]]; then
  echo "LocalStack did not publish port 4566" >&2
  exit 1
fi
wait_for_http "http://localhost:$LS_PORT/_localstack/health" 60

bash "$ROOT/scripts/perf-baseline-seed.sh" "$NET"

NUGET_URL="http://wiremock:8080/nuget/"
GITHUB_URL="http://wiremock:8080/github/"
if [[ "$UPSTREAM" == "real" ]]; then
  NUGET_URL="https://api.nuget.org/"
  GITHUB_URL="https://api.github.com/"
fi

START_NS=$(date +%s%N)
docker run -d --name bs-perf-lambda --network "$NET" -p 9000:8080 \
  -e DOTNET_ENVIRONMENT=Production \
  -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_REGION=us-east-1 -e AWS_DEFAULT_REGION=us-east-1 \
  -e AWS_ENDPOINT_URL_DYNAMODB=http://localstack:4566 \
  -e AWS_ENDPOINT_URL_SECRETS_MANAGER=http://localstack:4566 \
  -e AWS_RESOURCE_TEST_RESULTS_TABLE=badge-smith-test-result \
  -e AWS_RESOURCE_NONCE_TABLE=badge-smith-hmac-nonce \
  -e AWS_RESOURCE_ORG_SECRETS_TABLE=badge-smith-github-org-secrets \
  -e HTTP_NUGET_BASE_URL="$NUGET_URL" -e HTTP_GITHUB_BASE_URL="$GITHUB_URL" \
  "$IMG" >/dev/null

until curl -s -o /dev/null -w '%{http_code}' -XPOST http://localhost:9000/2015-03-31/functions/function/invocations \
  -d '{"version":"2.0","routeKey":"$default","rawPath":"/health","headers":{},"requestContext":{"http":{"method":"GET","path":"/health"},"stage":"$default","requestId":"warm"},"isBase64Encoded":false}' | grep -q 200; do
  sleep 0.2
done
READY_MS=$(( ($(date +%s%N) - START_NS) / 1000000 ))
RSS_IDLE=$(docker stats --no-stream --format '{{.MemUsage}}' bs-perf-lambda | awk '{print $1}')

echo "== k6 =="
K6_JSON_ARG="$K6_JSON"
K6_SCRIPT_ARG="$ROOT/scripts/k6-perf-test.js"
K6_API_URL="http://localhost:9000"
if [[ "$K6_MODE" == "docker" ]]; then
  K6_JSON_ARG="/artifacts/k6-summary.json"
  K6_SCRIPT_ARG="/work/scripts/k6-perf-test.js"
  K6_API_URL="http://bs-perf-lambda:8080"
fi
if [[ "$K6_NEEDS_WIN_PATHS" == "true" ]]; then
  K6_JSON_ARG="$(to_host_path "$K6_JSON")"
  K6_SCRIPT_ARG="$(to_host_path "$K6_SCRIPT_ARG")"
fi
RSS_PEAK_FILE="$ROOT/artifacts/rss-peak-kb.txt"
RSS_PEAK_KB=0
echo "$RSS_PEAK_KB" > "$RSS_PEAK_FILE"
(
  while true; do
    CUR=$(docker stats --no-stream --format '{{.MemUsage}}' bs-perf-lambda | awk '{print $1}')
    CUR_KB=$(mem_to_kb "$CUR")
    if (( CUR_KB > RSS_PEAK_KB )); then
      RSS_PEAK_KB=$CUR_KB
      echo "$RSS_PEAK_KB" > "$RSS_PEAK_FILE"
    fi
    sleep 1
  done
) &
STATS_PID=$!
K6_EXIT=0
K6_LOG="$ROOT/artifacts/k6-run.log"
if [[ "$K6_MODE" == "windows" ]]; then
  K6_COMMAND="k6.exe run --summary-export $K6_JSON_ARG -e K6_API_URL=$K6_API_URL -e K6_TARGET_MODE=rie -e K6_VUS=$VUS -e K6_DURATION=$DURATION $K6_SCRIPT_ARG"
  cmd.exe /C "$K6_COMMAND" > "$K6_LOG" 2>&1 || K6_EXIT=$?
else
  "${K6[@]}" run --summary-export "$K6_JSON_ARG" -e K6_API_URL="$K6_API_URL" -e K6_TARGET_MODE=rie \
    -e K6_VUS="$VUS" -e K6_DURATION="$DURATION" "$K6_SCRIPT_ARG" > "$K6_LOG" 2>&1 || K6_EXIT=$?
fi
cat "$K6_LOG"
kill "$STATS_PID" >/dev/null 2>&1 || true
wait "$STATS_PID" >/dev/null 2>&1 || true
RSS_PEAK_KB="$(cat "$RSS_PEAK_FILE")"
if [[ ! -s "$K6_JSON" ]]; then
  echo "k6 did not write summary export: $K6_JSON" >&2
  exit 1
fi
if (( K6_EXIT != 0 )); then
  echo "k6 failed with exit code $K6_EXIT" >&2
  exit "$K6_EXIT"
fi

OUT_ARG="$OUT"
K6_JSON_PY="$K6_JSON"
if [[ "$PYTHON_NEEDS_WIN_PATHS" == "true" ]]; then
  OUT_ARG="$(to_host_path "$OUT")"
  K6_JSON_PY="$(to_host_path "$K6_JSON")"
fi
GIT_SHA="$(git rev-parse --short HEAD)"
"${PYTHON[@]}" - "$OUT_ARG" "$K6_JSON_PY" <<EOF
import json, sys
out, k6file = sys.argv[1], sys.argv[2]
with open(k6file, encoding="utf-8") as f:
    k6 = json.load(f)
def metric(name):
    data = k6["metrics"].get(name, {})
    return data.get("values", data)
m = metric("http_req_duration")
http_reqs = metric("http_reqs")
http_failed = metric("http_req_failed")
json.dump({
  "date": "$STAMP", "label": "$LABEL",
  "gitSha": "$GIT_SHA",
  "arch": "$ARCH", "upstream": "$UPSTREAM",
  "image": {"binaryBytes": int("$BIN_BYTES"), "zipBytes": int("$ZIP_BYTES"), "mstat": "artifacts/mstat/bootstrap.mstat"},
  "boot": {"startToReadyMs": int("$READY_MS")},
  "k6": {"p50Ms": m.get("med"), "p95Ms": m.get("p(95)"), "p99Ms": m.get("p(99)"),
          "rps": http_reqs.get("rate"), "errorRate": http_failed.get("rate", http_failed.get("value", 0))},
  "memory": {"rssIdle": "$RSS_IDLE", "rssPeakKb": int("$RSS_PEAK_KB")},
}, open(out, "w", encoding="utf-8"), indent=2)
print("wrote", out)
EOF

cleanup
trap - EXIT
