#!/usr/bin/env bash
set -euo pipefail

LABEL="baseline"
UPSTREAM="mock"
ARCH="amd64"
VUS="${K6_VUS:-1}"
DURATION="${K6_DURATION:-60s}"

require_value() {
  local option="$1"
  local value="${2-}"
  if [[ -z "$value" || "$value" == --* ]]; then
    echo "$option requires a value" >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --label) require_value "$1" "${2-}"; LABEL="$2"; shift 2 ;;
    --upstream) require_value "$1" "${2-}"; UPSTREAM="$2"; shift 2 ;;
    --arch) require_value "$1" "${2-}"; ARCH="$2"; shift 2 ;;
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

if [[ "$UPSTREAM" == "real" && -z "${GITHUB_TOKEN:-}" ]]; then
  echo "GITHUB_TOKEN is required when --upstream real so GitHub package checks can authenticate" >&2
  exit 1
fi

RID="linux-x64"
PLATFORM="linux/amd64"
LAMBDA_ARCHITECTURE="x86_64"
if [[ "$ARCH" == "arm64" ]]; then
  RID="linux-arm64"
  PLATFORM="linux/arm64"
  LAMBDA_ARCHITECTURE="arm64"
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
NET="bs-perf-net"
K6_JSON="$ROOT/artifacts/k6-summary.json"
CDK_OUTPUTS="$ROOT/artifacts/perf-cdk-outputs.json"

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
  local exit_code="${1:-$?}"
  if (( exit_code != 0 )); then
    docker logs bs-perf-ls > "$ROOT/artifacts/perf-localstack.log" 2>&1 || true
    docker logs bs-perf-wm > "$ROOT/artifacts/perf-wiremock.log" 2>&1 || true
  fi
  docker ps -aq --filter "network=$NET" | xargs -r docker rm -f >/dev/null 2>&1 || true
  docker rm -f bs-perf-wm bs-perf-ls >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  return "$exit_code"
}
trap 'exit_code=$?; cleanup "$exit_code"; exit $exit_code' EXIT

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

validate_k6_summary() {
  "${PYTHON[@]}" - "$1" <<'PY'
import json
import sys

summary_path = sys.argv[1]
with open(summary_path, encoding="utf-8") as f:
    summary = json.load(f)

checks = summary.get("metrics", {}).get("checks")
if not checks:
    print("k6 summary did not contain aggregate checks metric", file=sys.stderr)
    raise SystemExit(1)

values = checks.get("values", checks)
rate = values.get("rate", values.get("value"))
passes = values.get("passes", 0)
fails = values.get("fails", 0)
if rate is None:
    total = passes + fails
    if total == 0:
        print("k6 summary checks metric did not contain check counts", file=sys.stderr)
        raise SystemExit(1)
    rate = passes / total

if fails != 0 or rate < 1.0:
    print(f"k6 checks failed: rate={rate}, passes={passes}, fails={fails}", file=sys.stderr)
    raise SystemExit(1)
PY
}

localstack_host_port() {
  docker port bs-perf-ls 4566/tcp 2>/dev/null | head -1 | sed 's/.*://'
}

command_is_wsl_windows_interop() {
  local command_path
  command_path="$(command -v "$1" 2>/dev/null || true)"
  [[ "$command_path" == /mnt/[a-zA-Z]/* ]]
}

cdk_wslenv() {
  local required="AWS_ACCESS_KEY_ID:AWS_SECRET_ACCESS_KEY:AWS_DEFAULT_REGION:AWS_REGION:CDK_DEFAULT_ACCOUNT:CDK_DEFAULT_REGION:AWS_ENDPOINT_URL:AWS_ENDPOINT_URL_S3:LOCALSTACK_HOST"
  if [[ -n "${WSLENV:-}" ]]; then
    printf '%s:%s\n' "$required" "$WSLENV"
  else
    printf '%s\n' "$required"
  fi
}

deploy_performance_stack() {
  local outputs_file="$1"
  local localstack_port="$2"
  local outputs_arg="$outputs_file"
  local cdk_local=(npx -y -p aws-cdk-local@3.0.4 -p aws-cdk@2.1129.0 cdklocal)
  local cdk_uses_windows_interop=false
  if command_is_wsl_windows_interop npx; then
    cdk_uses_windows_interop=true
  fi

  if [[ "$HOST_ROOT" != "$ROOT" || "$cdk_uses_windows_interop" == true ]]; then
    outputs_arg="$(to_host_path "$outputs_file")"
  fi

  rm -f "$outputs_file"
  pushd "$ROOT/build" >/dev/null
  env AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1 AWS_REGION=us-east-1 \
    CDK_DEFAULT_ACCOUNT=000000000000 CDK_DEFAULT_REGION=us-east-1 \
    AWS_ENDPOINT_URL="http://localhost:$localstack_port" AWS_ENDPOINT_URL_S3="http://s3.localhost.localstack.cloud:$localstack_port" LOCALSTACK_HOST="localhost:$localstack_port" \
    WSLENV="$(cdk_wslenv)" \
    "${cdk_local[@]}" bootstrap aws://000000000000/us-east-1 \
      -c stack=local-performance \
      -c lambdaZipPath="../artifacts/badge-lambda-$RID.zip" \
      -c lambdaArchitecture="$LAMBDA_ARCHITECTURE" \
      -c httpNuGetBaseUrl="$NUGET_URL" \
      -c httpGitHubBaseUrl="$GITHUB_URL" \
      -c localStackEndpoint=http://localstack:4566

  env AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1 AWS_REGION=us-east-1 \
    CDK_DEFAULT_ACCOUNT=000000000000 CDK_DEFAULT_REGION=us-east-1 \
    AWS_ENDPOINT_URL="http://localhost:$localstack_port" AWS_ENDPOINT_URL_S3="http://s3.localhost.localstack.cloud:$localstack_port" LOCALSTACK_HOST="localhost:$localstack_port" \
    WSLENV="$(cdk_wslenv)" \
    "${cdk_local[@]}" deploy BadgeSmithPerformanceStack \
      --require-approval never \
      --outputs-file "$outputs_arg" \
      -c stack=local-performance \
      -c lambdaZipPath="../artifacts/badge-lambda-$RID.zip" \
      -c lambdaArchitecture="$LAMBDA_ARCHITECTURE" \
      -c httpNuGetBaseUrl="$NUGET_URL" \
      -c httpGitHubBaseUrl="$GITHUB_URL" \
      -c localStackEndpoint=http://localstack:4566
  popd >/dev/null
}

read_api_url() {
  "${PYTHON[@]}" - "$1" "$2" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as f:
    outputs = json.load(f)

try:
    stack = outputs["BadgeSmithPerformanceStack"]
except KeyError as exc:
    print(f"missing CDK output: {exc}", file=sys.stderr)
    raise SystemExit(1)

url = stack.get("BadgeSmithApiUrl")
if not url or url == "unknown":
    url = stack.get("BadgeSmithLambdaFunctionUrl")
    if not url or url == "unknown":
        print("CDK output did not contain a usable API Gateway URL or Function URL", file=sys.stderr)
        raise SystemExit(1)

if url.startswith("https://") and "localhost.localstack.cloud:4566" in url:
    url = "http://" + url[len("https://"):]
if "localhost.localstack.cloud:4566" in url:
    url = url.replace("localhost.localstack.cloud:4566", f"localhost.localstack.cloud:{sys.argv[2]}")

print(url.rstrip("/"))
PY
}

find_new_worker_container() {
  local before_file="$1"
  local id name
  while IFS= read -r id; do
    [[ -n "$id" ]] || continue
    if grep -qx "$id" "$before_file"; then
      continue
    fi
    name="$(docker inspect -f '{{.Name}}' "$id" 2>/dev/null | sed 's#^/##')"
    if [[ "$name" == "bs-perf-ls" || "$name" == "bs-perf-wm" ]]; then
      continue
    fi
    echo "$id"
    return 0
  done < <(docker ps -aq)
}

echo "== build artifacts =="
docker build --platform "$PLATFORM" -f "$ROOT/src/BadgeSmith.Api/Dockerfile" --build-arg RID="$RID" --build-arg MSTAT=true --target export-mstat -o "$ROOT/artifacts/mstat" "$ROOT"
docker build --platform "$PLATFORM" -f "$ROOT/src/BadgeSmith.Api/Dockerfile" --build-arg RID="$RID" --target export-zip -o "$ROOT/artifacts" "$ROOT"
ZIP="$ROOT/artifacts/badge-lambda-$RID.zip"
ZIP_BYTES=$(stat -c%s "$ZIP" 2>/dev/null || stat -f%z "$ZIP")
BIN_BYTES=$(unzip -l "$ZIP" | awk '/bootstrap$/ {print $1}')

NUGET_URL="http://wiremock:8080/nuget/"
GITHUB_URL="http://wiremock:8080/github/"
if [[ "$UPSTREAM" == "real" ]]; then
  NUGET_URL="https://api.nuget.org/"
  GITHUB_URL="https://api.github.com/"
fi

echo "== boot LocalStack =="
cleanup 0
docker network create "$NET" >/dev/null
docker run -d --name bs-perf-ls --network "$NET" --network-alias localstack -p 4566 \
  -e DEBUG=1 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  localstack/localstack:4.6 >/dev/null

if [[ "$UPSTREAM" == "mock" ]]; then
  echo "== boot WireMock =="
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
  docker run -d --name bs-perf-wm --network "$NET" --network-alias wiremock \
    -v "$WIREMOCK_ROOT:/home/wiremock:ro" wiremock/wiremock:3.9.1 >/dev/null
fi

LS_PORT=""
for _ in {1..60}; do
  LS_PORT="$(localstack_host_port)"
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

echo "== deploy CDK stack =="
deploy_performance_stack "$CDK_OUTPUTS" "$LS_PORT"
K6_API_URL="$(read_api_url "$CDK_OUTPUTS" "$LS_PORT")"

echo "== seed LocalStack data =="
bash "$ROOT/scripts/perf-baseline-seed.sh" "$NET"

CONTAINERS_BEFORE_HEALTH="$ROOT/artifacts/perf-containers-before-health.txt"
docker ps -aq | sort > "$CONTAINERS_BEFORE_HEALTH"
START_NS=$(date +%s%N)
wait_for_http "$K6_API_URL/health" 120
READY_MS=$(( ($(date +%s%N) - START_NS) / 1000000 ))

WORKER_CONTAINER_ID="$(find_new_worker_container "$CONTAINERS_BEFORE_HEALTH" || true)"
RSS_IDLE=""
RSS_IDLE_KB=""
RSS_PEAK_KB=""
MEMORY_SOURCE="not-attributed-localstack"
if [[ -n "$WORKER_CONTAINER_ID" ]]; then
  MEMORY_SOURCE="docker-stats-localstack-lambda-worker"
  RSS_IDLE="$(docker stats --no-stream --format '{{.MemUsage}}' "$WORKER_CONTAINER_ID" | awk '{print $1}')"
  RSS_IDLE_KB="$(mem_to_kb "$RSS_IDLE")"
  RSS_PEAK_KB="$RSS_IDLE_KB"
fi

echo "== k6 =="
K6_JSON_ARG="$K6_JSON"
K6_SCRIPT_ARG="$ROOT/scripts/k6-perf-test.js"
if [[ "$K6_NEEDS_WIN_PATHS" == "true" ]]; then
  K6_JSON_ARG="$(to_host_path "$K6_JSON")"
  K6_SCRIPT_ARG="$(to_host_path "$K6_SCRIPT_ARG")"
fi
RSS_PEAK_FILE="$ROOT/artifacts/rss-peak-kb.txt"
K6_LOG="$ROOT/artifacts/k6-run.log"
rm -f "$K6_JSON" "$K6_LOG" "$RSS_PEAK_FILE"
echo "${RSS_PEAK_KB:-0}" > "$RSS_PEAK_FILE"
STATS_PID=""
if [[ -n "$WORKER_CONTAINER_ID" ]]; then
  (
    RSS_PEAK_KB="${RSS_PEAK_KB:-0}"
    while true; do
      CUR=$(docker stats --no-stream --format '{{.MemUsage}}' "$WORKER_CONTAINER_ID" | awk '{print $1}')
      CUR_KB=$(mem_to_kb "$CUR")
      if (( CUR_KB > RSS_PEAK_KB )); then
        RSS_PEAK_KB=$CUR_KB
        echo "$RSS_PEAK_KB" > "$RSS_PEAK_FILE"
      fi
      sleep 1
    done
  ) &
  STATS_PID=$!
fi
K6_EXIT=0
if [[ "$K6_MODE" == "windows" ]]; then
  K6_COMMAND="k6.exe run --summary-export $K6_JSON_ARG -e K6_API_URL=$K6_API_URL -e K6_VUS=$VUS -e K6_DURATION=$DURATION $K6_SCRIPT_ARG"
  cmd.exe /C "$K6_COMMAND" > "$K6_LOG" 2>&1 || K6_EXIT=$?
else
  "${K6[@]}" run --summary-export "$K6_JSON_ARG" -e K6_API_URL="$K6_API_URL" \
    -e K6_VUS="$VUS" -e K6_DURATION="$DURATION" "$K6_SCRIPT_ARG" > "$K6_LOG" 2>&1 || K6_EXIT=$?
fi
cat "$K6_LOG"
if [[ -n "$STATS_PID" ]]; then
  kill "$STATS_PID" >/dev/null 2>&1 || true
  wait "$STATS_PID" >/dev/null 2>&1 || true
fi
RSS_PEAK_KB="$(cat "$RSS_PEAK_FILE")"
if [[ ! -s "$K6_JSON" ]]; then
  echo "k6 did not write summary export: $K6_JSON" >&2
  exit 1
fi
if (( K6_EXIT != 0 )); then
  echo "k6 failed with exit code $K6_EXIT" >&2
  exit "$K6_EXIT"
fi
K6_JSON_CHECK="$K6_JSON"
if [[ "$PYTHON_NEEDS_WIN_PATHS" == "true" ]]; then
  K6_JSON_CHECK="$(to_host_path "$K6_JSON")"
fi
validate_k6_summary "$K6_JSON_CHECK"

OUT_ARG="$OUT"
K6_JSON_PY="$K6_JSON"
if [[ "$PYTHON_NEEDS_WIN_PATHS" == "true" ]]; then
  OUT_ARG="$(to_host_path "$OUT")"
  K6_JSON_PY="$(to_host_path "$K6_JSON")"
fi
GIT_SHA="$(git rev-parse --short HEAD)"
"${PYTHON[@]}" - "$OUT_ARG" "$K6_JSON_PY" "$STAMP" "$LABEL" "$GIT_SHA" "$ARCH" "$UPSTREAM" "$BIN_BYTES" "$ZIP_BYTES" "$READY_MS" "$RSS_IDLE_KB" "$RSS_PEAK_KB" "$MEMORY_SOURCE" "$WORKER_CONTAINER_ID" <<'PY'
import json, sys
out, k6file, stamp, label, git_sha, arch, upstream, bin_bytes, zip_bytes, ready_ms, rss_idle_kb, rss_peak_kb, memory_source, container_id = sys.argv[1:]
with open(k6file, encoding="utf-8") as f:
    k6 = json.load(f)
def metric(name):
    data = k6["metrics"].get(name, {})
    return data.get("values", data)
def kb_to_mb(value):
    return round(int(value) / 1024, 3)
m = metric("http_req_duration")
http_reqs = metric("http_reqs")
http_failed = metric("http_req_failed")
if memory_source == "docker-stats-localstack-lambda-worker" and container_id:
    memory = {
        "rssIdleMb": kb_to_mb(rss_idle_kb),
        "rssPeakMb": kb_to_mb(rss_peak_kb),
        "source": memory_source,
        "containerId": container_id,
    }
else:
    memory = {"rssIdleMb": None, "rssPeakMb": None, "source": "not-attributed-localstack"}
json.dump({
  "date": stamp, "label": label,
  "gitSha": git_sha,
  "arch": arch, "upstream": upstream,
  "image": {"binaryBytes": int(bin_bytes), "zipBytes": int(zip_bytes), "mstat": "artifacts/mstat/bootstrap.mstat"},
  "boot": {"startToReadyMs": int(ready_ms)},
  "k6": {"p50Ms": m.get("med"), "p95Ms": m.get("p(95)"), "p99Ms": m.get("p(99)"),
          "rps": http_reqs.get("rate"), "errorRate": http_failed.get("rate", http_failed.get("value", 0))},
  "memory": memory,
}, open(out, "w", encoding="utf-8"), indent=2)
print("wrote", out)
PY

cleanup 0
trap - EXIT
