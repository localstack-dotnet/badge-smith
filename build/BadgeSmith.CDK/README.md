# BadgeSmith CDK apps

BadgeSmith uses two separate CDK app entrypoints. Keep them separate: CDK constructs the
whole app tree before CLI stack selectors are applied, and the production and
local-performance stacks use different environments and Lambda ZIP assets.

| Boundary | Runtime ID | Lambda asset | Validation environment |
| --- | --- | --- | --- |
| Local performance | `linux-x64` | `artifacts/badge-lambda-linux-x64.zip` | Local x64 AOT build and LocalStack synth |
| Production | `linux-arm64` | `artifacts/badge-lambda-linux-arm64.zip` | ARM64-capable builder and hosted ARM64 CI |

Neither app accepts a topology-selection context such as
`-c stack=local-performance`. Select topology by using the app's project/working
directory and its native stack ID.

## Production app

- Project: `build/BadgeSmith.CDK/BadgeSmith.CDK.csproj`
- CDK working directory: `build`
- CDK config: `build/cdk.json`
- Native stack ID: `BadgeSmithStack`
- Lambda ZIP default: `../artifacts/badge-lambda-linux-arm64.zip`

Build and synthesize production infrastructure:

```bash
dotnet build build/BadgeSmith.CDK/BadgeSmith.CDK.csproj -c Release
tools/badgesmith.cs lambda build --target zip --rid linux-arm64 --clean --verbose
cd build
cdk ls
cdk synth BadgeSmithStack
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

## Local performance app

- Project: `build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj`
- CDK working directory: `build/BadgeSmith.CDK.LocalPerformance`
- CDK config: `build/BadgeSmith.CDK.LocalPerformance/cdk.json`
- Native stack ID: `BadgeSmithPerformanceStack`
- Lambda ZIP default: `../../artifacts/badge-lambda-linux-x64.zip`

Build and synthesize LocalStack-only performance infrastructure:

```bash
dotnet build build/BadgeSmith.CDK.LocalPerformance/BadgeSmith.CDK.LocalPerformance.csproj -c Release
tools/badgesmith.cs lambda build --target zip --rid linux-x64 --verbose
cd build/BadgeSmith.CDK.LocalPerformance
cdklocal synth BadgeSmithPerformanceStack
```

The local-performance app is for LocalStack benchmarking only. Never deploy it to AWS.
Its x64 ZIP build and `cdklocal synth` are the normal local AOT/infrastructure checks.

## Context values

Both apps accept `account` and `region` context values. Production otherwise uses the CDK
default account and region environment variables. The local-performance app defaults to
account `000000000000` and region `us-east-1`.

The local-performance app also accepts these context values:

- `lambdaZipPath` (default `../../artifacts/badge-lambda-linux-x64.zip`)
- `lambdaArchitecture` (`x86_64` or `arm64`, default `x86_64`)
- `localStackEndpoint` (default `http://localstack:4566`)
- `httpNuGetBaseUrl` (default `https://api.nuget.org/`)
- `httpGitHubBaseUrl` (default `https://api.github.com/`)
