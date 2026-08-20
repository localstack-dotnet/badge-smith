# BadgeSmith local-performance CDK app

This project owns the LocalStack-only infrastructure used for repeatable local
performance measurements. It is separate from the [production CDK app](../BadgeSmith.CDK/README.md)
because the apps use different environments and Lambda ZIP architectures.

- Project: `build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj`
- CDK working directory: `build/BadgeSmith.CDK.LocalPerformance`
- CDK config: `build/BadgeSmith.CDK.LocalPerformance/cdk.json`
- Native stack ID: `BadgeSmithPerformanceStack`
- Lambda ZIP default: `../../artifacts/badge-lambda-linux-x64.zip`
- Upstream mode: explicit `Live`

The production deployment workflow owns its pinned Node.js and AWS CDK CLI versions. This local
workflow additionally requires `cdklocal`, which the repository does not currently pin. Record the
local CLI versions with any published measurement so the environment remains reproducible.

Build and synthesize the local infrastructure:

```bash
dotnet build build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj -c Release
tools/badgesmith.cs lambda build --target zip --rid linux-x64 --verbose
cd build/BadgeSmith.CDK.LocalPerformance
cdklocal synth BadgeSmithPerformanceStack \
  --context account=000000000000 \
  --context region=us-east-1
```

Account and region are required from CDK context or the `CDK_DEFAULT_ACCOUNT` and
`CDK_DEFAULT_REGION` environment variables. The app fails before synthesis when either
value is missing.

Additional context values:

- `lambdaZipPath` (default `../../artifacts/badge-lambda-linux-x64.zip`)
- `lambdaArchitecture` (`x86_64` or `arm64`, default `x86_64`)
- `localStackEndpoint` (default `http://localstack:4566`)
- `httpNuGetBaseUrl` (default `https://api.nuget.org/`)
- `httpGitHubBaseUrl` (default `https://api.github.com/`)

Never deploy this app to AWS. Its x64 ZIP build and `cdklocal synth` are the normal
local AOT and infrastructure checks.
