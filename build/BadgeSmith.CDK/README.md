# BadgeSmith production CDK app

This project owns only the production stack. The separate LocalStack benchmark app is
documented in [../BadgeSmith.CDK.LocalPerformance/README.md](../BadgeSmith.CDK.LocalPerformance/README.md).
Do not add topology-selection context back to either app; CDK constructs the whole app
tree before CLI stack selectors are applied.

The deployment workflow is the source of truth for pinned Node.js and AWS CDK CLI
versions. Use the same versions for local synth and diff checks instead of duplicating
version numbers in documentation.

- Project: `build/BadgeSmith.CDK/BadgeSmith.CDK.csproj`
- CDK working directory: `build`
- CDK config: `build/cdk.json`
- Native stack ID: `BadgeSmithStack`
- Lambda ZIP default: `../artifacts/badge-lambda-linux-arm64.zip`
- Upstream mode: explicit `Live`

Build and synthesize production infrastructure:

```bash
dotnet build build/BadgeSmith.CDK/BadgeSmith.CDK.csproj -c Release
tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
cd build
cdk ls
cdk synth BadgeSmithStack \
  --context account=<aws-account-id> \
  --context region=eu-central-1
```

The ARM64 ZIP is not part of the ordinary local test loop. Building it locally requires
an ARM64 host or a buildx builder with ARM64 execution support; hosted CI owns the
required production artifact check. The PR CI workflow builds the ZIP but does not run
production CDK synth, so `cdk synth BadgeSmithStack` remains a separate infrastructure
gate.

Production deploy remains approval-gated and must target the single production stack:

```bash
cd build
cdk deploy BadgeSmithStack --require-approval never
```

Do not use `--all` for production synth, diff, or deploy commands.
