# BadgeSmith production CDK app

This project owns only the production stack. The separate LocalStack benchmark app is
documented in [../BadgeSmith.CDK.LocalPerformance/README.md](../BadgeSmith.CDK.LocalPerformance/README.md).
Do not add topology-selection context back to either app; CDK constructs the whole app
tree before CLI stack selectors are applied.

The deployment workflow is the source of truth for pinned Node.js and AWS CDK CLI
versions. Use the same versions for local synth and diff checks instead of duplicating
version numbers in documentation.

The CloudFront cache/transport contract (zero-TTL invariants, error-caching behavior,
cache-key rules) is owned by [`ARCHITECTURE.md`](../../ARCHITECTURE.md#cache-strategy);
the exact property values live in `BadgeSmithCloudFrontFactory` and `ProductionStack` under
`build/BadgeSmith.CDK.Shared/`, and the in-process assertions that lock them live in
[`tests/BadgeSmith.CDK.Tests`](../../tests/BadgeSmith.CDK.Tests/README.md).

Behavior-neutral CDK extraction must preserve CloudFormation logical IDs: any synthesized
replacement, logical-ID change, or property drift is a blocker. Prove neutrality with a
before/after `cdk synth BadgeSmithStack` template diff before committing the change.

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

`--require-approval never` disables CDK's interactive prompt; it does not waive the repository
approval gate or any external AWS/environment approval.

Do not use `--all` for production synth, diff, or deploy commands.
