#!/usr/bin/env bash
set -euo pipefail

# scripts/test-ingestion.sh
# Test BadgeSmith HMAC authentication and test result ingestion

BASE_URL=""
OWNER=""
REPO=""
PLATFORM=""
BRANCH=""
SECRET=""
PAYLOAD_FILE=""
PAYLOAD=""
VERBOSE=0

usage() {
  cat <<'EOF'
Test BadgeSmith HMAC authentication and test result ingestion.

USAGE:
  scripts/test-ingestion.sh --base-url <url> --owner <owner> --repo <repo>
                            --platform <platform> --branch <branch> --secret <secret>
                            [--payload-file <file>] [--payload <json>] [--verbose]

OPTIONS:
  --base-url     Base URL of the API (e.g., http://localhost:9474)
  --owner        Repository owner/organization
  --repo         Repository name
  --platform     Platform (linux/windows/macos)
  --branch       Branch name
  --secret       HMAC secret for authentication
  --payload-file Path to JSON file containing test results
  --payload      Inline JSON payload (alternative to --payload-file)
  --verbose      Show detailed request information
  -h, --help     Show this help

EXAMPLES:
  # Using payload file
  scripts/test-ingestion.sh --base-url "http://localhost:9474" \
    --owner "localstack-dotnet" --repo "localstack.client" \
    --platform "linux" --branch "main" --secret "your-hmac-secret" \
    --payload-file "test-payload.json"

  # Using inline payload
  scripts/test-ingestion.sh --base-url "http://localhost:9474" \
    --owner "localstack-dotnet" --repo "localstack.client" \
    --platform "linux" --branch "main" --secret "your-hmac-secret" \
    --payload '{"platform":"Linux","passed":190,"failed":0,"skipped":0,"total":190,...}'

PAYLOAD FORMAT:
  {
    "platform": "Linux",
    "passed": 190,
    "failed": 0,
    "skipped": 0,
    "total": 190,
    "url_html": "https://github.com/owner/repo/runs/123",
    "timestamp": "2025-09-05T10:57:00Z",
    "commit": "4d8474bda0b16fbbb69887d0d08c3885843bbdc7",
    "run_id": "16814735762",
    "workflow_run_url": "https://github.com/owner/repo/actions/runs/456"
  }
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)     BASE_URL="${2:-}"; shift 2;;
    --owner)        OWNER="${2:-}"; shift 2;;
    --repo)         REPO="${2:-}"; shift 2;;
    --platform)     PLATFORM="${2:-}"; shift 2;;
    --branch)       BRANCH="${2:-}"; shift 2;;
    --secret)       SECRET="${2:-}"; shift 2;;
    --payload-file) PAYLOAD_FILE="${2:-}"; shift 2;;
    --payload)      PAYLOAD="${2:-}"; shift 2;;
    --verbose)      VERBOSE=1; shift;;
    -h|--help)      usage; exit 0;;
    *) echo "Unknown argument: $1"; usage; exit 2;;
  esac
done

# Validate required parameters
if [[ -z "$BASE_URL" || -z "$OWNER" || -z "$REPO" || -z "$PLATFORM" || -z "$BRANCH" || -z "$SECRET" ]]; then
  echo "❌ Missing required parameters"
  usage
  exit 1
fi

if [[ -z "$PAYLOAD_FILE" && -z "$PAYLOAD" ]]; then
  echo "❌ Either --payload-file or --payload must be provided"
  usage
  exit 1
fi

if [[ -n "$PAYLOAD_FILE" && -n "$PAYLOAD" ]]; then
  echo "❌ Cannot specify both --payload-file and --payload"
  usage
  exit 1
fi

# Load payload
if [[ -n "$PAYLOAD_FILE" ]]; then
  if [[ ! -f "$PAYLOAD_FILE" ]]; then
    echo "❌ Payload file not found: $PAYLOAD_FILE"
    exit 1
  fi
  PAYLOAD_JSON=$(cat "$PAYLOAD_FILE")
else
  PAYLOAD_JSON="$PAYLOAD"
fi

# Normalize parameters (lowercase)
OWNER=$(echo "$OWNER" | tr '[:upper:]' '[:lower:]')
REPO=$(echo "$REPO" | tr '[:upper:]' '[:lower:]')
PLATFORM=$(echo "$PLATFORM" | tr '[:upper:]' '[:lower:]')
BRANCH=$(echo "$BRANCH" | tr '[:upper:]' '[:lower:]')

# Generate authentication headers
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.%3NZ")
NONCE=$(uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]')

# Compute HMAC-SHA256 signature
SIGNATURE="sha256=$(echo -n "$PAYLOAD_JSON" | openssl dgst -sha256 -hmac "$SECRET" -binary | xxd -p -c 256)"

# Build request URL
URL="$BASE_URL/tests/results/$PLATFORM/$OWNER/$REPO/$BRANCH"

if [[ $VERBOSE -eq 1 ]]; then
  echo "🚀 Sending test result ingestion request"
  echo "URL: $URL"
  echo "Headers:"
  echo "  Content-Type: application/json"
  echo "  X-Signature: $SIGNATURE"
  echo "  X-Timestamp: $TIMESTAMP"
  echo "  X-Nonce: $NONCE"
  echo "Payload:"
  echo "$PAYLOAD_JSON"
  echo ""
fi

# Send request using curl
HTTP_CODE=$(curl -s -w "%{http_code}" -o response.tmp \
  -X POST "$URL" \
  -H "Content-Type: application/json" \
  -H "X-Signature: $SIGNATURE" \
  -H "X-Timestamp: $TIMESTAMP" \
  -H "X-Nonce: $NONCE" \
  -d "$PAYLOAD_JSON")

RESPONSE_BODY=$(cat response.tmp)
rm -f response.tmp

if [[ "$HTTP_CODE" -ge 200 && "$HTTP_CODE" -lt 300 ]]; then
  echo "✅ Request successful! (HTTP $HTTP_CODE)"
  echo "Response:"
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
else
  echo "❌ Request failed! (HTTP $HTTP_CODE)"
  echo "Response:"
  echo "$RESPONSE_BODY" | jq . 2>/dev/null || echo "$RESPONSE_BODY"
  exit 1
fi
