#!/usr/bin/env bash
set -euo pipefail

# Manual deployment script using CDK
# Usage: ./scripts/deploy.sh [--build-lambda] [--diff] [--profile <aws-profile>]

BUILD_LAMBDA=0
SHOW_DIFF=0
AWS_PROFILE=""
VERBOSE=0

usage() {
    cat <<'EOF'
Deploy BadgeSmith to AWS using CDK.

USAGE:
  scripts/deploy.sh [options]

OPTIONS:
  --build-lambda    Build Lambda ZIP before deployment
  --diff           Show CDK diff before deployment
  --profile        AWS profile to use
  -v, --verbose    Verbose output
  -h, --help       Show this help

EXAMPLES:
  # Deploy with fresh Lambda build
  scripts/deploy.sh --build-lambda --diff

  # Deploy using specific AWS profile
  scripts/deploy.sh --profile production --build-lambda

  # Quick deploy (no build, no diff)
  scripts/deploy.sh
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --build-lambda) BUILD_LAMBDA=1; shift;;
        --diff)         SHOW_DIFF=1; shift;;
        --profile)      AWS_PROFILE="${2:-}"; shift 2;;
        -v|--verbose)   VERBOSE=1; shift;;
        -h|--help)      usage; exit 0;;
        *) echo "Unknown arg: $1"; usage; exit 2;;
    esac
done

log() { echo "🚀 $*"; }
run() { [[ $VERBOSE -eq 1 ]] && echo "+ $*" >&2; "$@"; }

# Setup AWS profile if specified
if [[ -n "$AWS_PROFILE" ]]; then
    export AWS_PROFILE
    log "Using AWS profile: $AWS_PROFILE"
fi

# Build Lambda if requested
if [[ $BUILD_LAMBDA -eq 1 ]]; then
    log "Building Lambda ZIP for ARM64..."
    run ./scripts/build-lambda.sh --target zip --rid linux-arm64 --clean --verbose
fi

# Check if Lambda ZIP exists
if [[ ! -f "artifacts/badge-lambda-linux-arm64.zip" ]]; then
    echo "❌ Lambda ZIP not found. Run with --build-lambda or build manually first."
    exit 1
fi

log "Lambda ZIP found: $(du -h artifacts/badge-lambda-linux-arm64.zip | cut -f1)"

# CDK synth to validate
log "Validating CDK templates..."
cd build/BadgeSmith.CDK
run cdk synth

# Show diff if requested
if [[ $SHOW_DIFF -eq 1 ]]; then
    log "Showing CDK diff..."
    run cdk diff || true
    echo
    read -p "Continue with deployment? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Deployment cancelled."
        exit 0
    fi
fi

# Deploy!
log "Deploying BadgeSmith stack..."
run cdk deploy --require-approval never

log "✅ Deployment complete!"

# Show outputs
log "Stack outputs:"
run cdk ls --long 2>/dev/null || true

echo
echo "🎉 BadgeSmith deployed successfully!"
echo "   Lambda ZIP: artifacts/badge-lambda-linux-arm64.zip"
echo "   Stack: $(cdk ls 2>/dev/null || echo 'BadgeSmithStack')"
