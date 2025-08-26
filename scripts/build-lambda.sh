#!/usr/bin/env bash
set -euo pipefail

TARGET="zip"                         # zip|image|both
RID="linux-x64"                      # linux-x64|linux-arm64
IMAGE_TAG="badgesmith-lambda:local"
DOCKERFILE="src/apps/BadgeSmith.Api/Dockerfile"
CONTEXT="."
OUT_DIR="artifacts"
PUSH=0
CLEAN=0
VERBOSE=0

usage() {
  cat <<'EOF'
Build BadgeSmith Lambda (zip and/or container image) via Docker Buildx.

USAGE:
  scripts/build-lambda.sh [options]

OPTIONS:
  -t, --target       zip|image|both           (default: zip)
  -r, --rid          linux-x64|linux-arm64    (default: linux-x64)
  -i, --image-tag    Docker image tag         (default: badgesmith-lambda:local)
  -f, --dockerfile   Path to Dockerfile       (default: src/apps/BadgeSmith.Api/Dockerfile)
  -c, --context      Build context            (default: .)
  -o, --out          Output dir for artifacts (default: artifacts)
      --push         Push image after build
      --clean        Clean output directory before writing
  -v, --verbose      Verbose docker commands
  -h, --help         Show this help

EXAMPLES:
  # Zip only (default RID linux-x64)
  scripts/build-lambda.sh --target zip --clean

  # Zip for ARM64
  scripts/build-lambda.sh --target zip --rid linux-arm64 --clean

  # Container image (don’t push)
  scripts/build-lambda.sh --target image --image-tag yourrepo/badgesmith:latest

  # Both zip + image, push image
  scripts/build-lambda.sh --target both \
    --image-tag <acct>.dkr.ecr.eu-central-1.amazonaws.com/badgesmith:latest --push
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -t|--target)     TARGET="${2:-}"; shift 2;;
    -r|--rid)        RID="${2:-}"; shift 2;;
    -i|--image-tag)  IMAGE_TAG="${2:-}"; shift 2;;
    -f|--dockerfile) DOCKERFILE="${2:-}"; shift 2;;
    -c|--context)    CONTEXT="${2:-}"; shift 2;;
    -o|--out)        OUT_DIR="${2:-}"; shift 2;;
    --push)          PUSH=1; shift;;
    --clean)         CLEAN=1; shift;;
    -v|--verbose)    VERBOSE=1; shift;;
    -h|--help)       usage; exit 0;;
    *) echo "Unknown arg: $1"; usage; exit 2;;
  esac
done

platform="linux/amd64"; [[ "$RID" == "linux-arm64" ]] && platform="linux/arm64"
[[ $CLEAN -eq 1 ]] && rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

run() { [[ $VERBOSE -eq 1 ]] && echo "+ docker $*" >&2; docker "$@"; }

if [[ "$TARGET" == "zip" || "$TARGET" == "both" ]]; then
  run buildx build \
    -f "$DOCKERFILE" \
    --target export-zip \
    --build-arg "RID=$RID" \
    --platform "$platform" \
    --output "type=local,dest=$OUT_DIR" \
    "$CONTEXT"
fi

if [[ "$TARGET" == "image" || "$TARGET" == "both" ]]; then
  args=( buildx build
    -f "$DOCKERFILE"
    --target lambda-image
    --build-arg "RID=$RID"
    --platform "$platform"
    -t "$IMAGE_TAG"
    "$CONTEXT"
  )
  [[ $PUSH -eq 1 ]] && args+=( --push )
  run "${args[@]}"
fi

echo "Done. Artifacts in '$OUT_DIR'."
