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

## CI artifact promotion

The manual [deployment workflow](../../.github/workflows/deploy.yml) runs from `master`
and promotes an existing successful, completed same-repository `ci-cd.yml` **push to
master**. It does not rebuild Lambda. Supply the exact `ci_run_id`, `ci_run_attempt`,
`ci_head_sha` (full SHA), and `ci_artifact_id` from the CI selection summary. There is
no latest-artifact, branch-search, or deploy-time build fallback.

An unchanged workflow skips a dispatch from a non-master ref without deploying;
a skipped job is not a successful deployment. The repository guard also depends
on hosted controls: the `production` environment must restrict deployment branches
to master, and AWS OIDC trust must constrain the intended repository, production
environment subject, and audience (normally `sts.amazonaws.com`). An environment
subject such as `repo:localstack-dotnet/badge-smith:environment:production` relies
on that environment's branch restriction. As observed on 2026-10-16, the
`production` environment has no deployment branch restriction and `master` is
unprotected ([evidence](../../docs/research/baselines/2026-10-16-github-delivery-trust.json));
AWS OIDC trust remains unverified; this documentation does not establish
enforcement. A modified workflow on another ref cannot be secured by its
own repository guard alone. Verifying or changing those settings is a separate gate.

CI names each immutable master artifact `lambda-zip-{run_id}-{run_attempt}`; PR
artifacts retain a numeric PR prefix followed by run and attempt. Each contains
`badge-lambda-linux-arm64.zip` and `provenance.json`; the latter records repository,
SHA, run, attempt, inner ZIP SHA-256, and exact ZIP bytes. Older artifacts without
this manifest are not promotable through this workflow. The current CI attempt
must still match the selected attempt; a rerun requires selecting its new artifact.
An explicitly selected older successful master run may be promoted when every
identity/provenance gate passes; selection is not restricted to the newest run or
current master tip. Older artifacts without the required manifest still fail.

Before AWS credential setup, delivery validates inputs, run metadata, the single
matching artifact ID, expiration, and GitHub's artifact **archive** SHA-256. It then
validates the manifest and the **inner Lambda ZIP** SHA-256 and exact byte count.
These are two distinct digests: GitHub's compressed download archive is not the
deployable ZIP. The reader accepts only the two expected flat regular files and
never extracts archive-provided paths onto disk. Download URLs are restricted to
GitHub's HTTPS Azure Blob artifact storage; GitHub credentials are not forwarded.
Missing evidence, unsupported archive layout, or a storage-host change fails closed.

The workflow checks out the verified source SHA into `source/` for CDK and places
the verified ZIP in `source/artifacts/`. Delivery control scripts stay in the
workflow checkout. Provenance, archive bytes, and ZIP bytes are recorded in the
Actions summary. Artifact bytes establish size only; they do not establish Lambda
INIT duration, memory, deployed identity, or production readiness.

## Diff and failure behavior

The workflow always runs this gate before deploy, with the pinned CLI:

```bash
cdk diff BadgeSmithStack --no-fail --method change-set
```

`--no-fail` permits infrastructure differences. Explicit `change-set` prevents
the CLI's default `auto` method from converting change-set failures into a
successful template-only diff. Stack-read and change-set failures block deployment;
the workflow adds no error-swallowing expression or alternate method.

This diff is **not an inherently read-only AWS operation**: it may publish template
assets and create/delete a CloudFormation change set. For a new stack it may also
create/delete an empty placeholder stack. A missing stack is a valid first-deploy
case and still goes through diff; an actual `DescribeStacks`/`GetTemplate`
AccessDenied is not treated as absence. The CLI can try its existing deployment
credentials when lookup-role setup fails. Existing role permissions must suffice;
insufficient permissions must surface as a failure, without widening IAM or adding
a template fallback.

The [dated CLI source evidence](../../docs/research/2026-10-16-delivery-cdk-diff-semantics.md)
records the inspected package identity, integrity, and source locations. Workflow
pins own the current version; revalidate semantics when changing them. Local shell
fixtures verify workflow propagation using stand-ins; they do not execute CDK or
prove AWS permissions. Hosted CDK execution remains a separate validation gate.

## Required GET smoke

Supply both workflow inputs before deployment:

- `smoke_test_badge_path`: an **existing**
  `/badges/tests/{platform}/{owner}/{repo}/{branch}` record.
- `smoke_nuget_badge_path`: an existing
  `/badges/packages/nuget/{package}` endpoint.

Segments use ASCII letters, digits, dots, hyphens, or underscores; `.` and `..`
segments are forbidden. Paths have no query or fragment; encode a slash within a
branch as `%2F`. No
badge identifiers, credentials, or production data are supplied by the helper.
Missing/unsafe configuration fails before AWS setup. A missing test record is a
failed smoke, not a reason to seed data or skip that check.

After deploy, `--outputs-file` supplies `BadgeSmithStack.BadgeSmithApiUrl` and
`CustomDomainUrl`. Smoke performs GET health, test badge, and NuGet badge on **both**
direct Gateway and the public domain. Health proves handler/serialization reachability,
not dependencies; the badge checks exercise the DynamoDB and NuGet paths.
HTTP 200 alone is insufficient: health and badge JSON contracts must match.
HTML, malformed JSON, redirects, error badges, and permanent HTTP errors fail.

All six checks share a 180-second budget. Each request, including body reading,
has at most 10 seconds; each check has at most five attempts. Only transient
HTTP/network failures retry. Unrecovered timeout, cancellation, deadline exhaustion, and
retry exhaustion fail the workflow. There are no writes, HMAC requests, data
seeding, or automatic rollback. A smoke failure means deployment may already have
changed production; investigate before taking any separately approved recovery action.

Failures report a finite owned category. Smoke also reports validated `gateway`
or `public` origin labels, `health`/`tests`/`nuget` check labels, attempt, and HTTP
status when available. No raw exception text, response body, URL, credentials, or
input paths are logged; unknown failures remain generic. Use the category to
identify the failed gate without exposing signed artifact URLs or request data.

Local verification uses only Node built-ins and injected fixtures:

```bash
node --test .github/scripts/delivery/delivery.test.mjs
```

Run it from the repository root. Fixtures do not contact GitHub, production HTTP,
or AWS. Live artifact promotion, hosted workflow execution, AWS permissions, and
post-deploy smoke still require separately authorized validation.
