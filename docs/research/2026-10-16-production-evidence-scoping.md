# Production runtime and performance evidence — scoping research

**Evidence snapshot: 2026-10-16.** Repository identity: `github.com/localstack-dotnet/badge-smith`;
local source inspected at `1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`. This is source/documentation
research for a scope interview, not a deployment plan, fresh production evidence, or approval.
Repository canon owns targets and authority; dated reports retain their original attribution.

The supplied research boundary is preserved here, not introduced as new repository policy:

> This is factual scoping research only. Current canon owns targets and approval. No AWS/GitHub authenticated requests, credential discovery, production HTTP, workflow dispatch, deploy, load or cold-start generation, builds/tests/benchmarks, installs, source checkout setup, product/policy/build/workflow/infrastructure edits, commits or push. No canceled environment required-reviewer/admin-bypass proposal may be revived. No field of the future increment is user-approved merely because a research note recommends it. Public official-documentation HTTP reads are allowed. Preserve unknowns and historical attribution. GLOSSARY remains domain-only; do not put evidence mechanics there.

## Current owners and starting point

- [Platform architecture](../architecture/platform-and-delivery.md#performance-goals), lines 149–194,
  owns INIT p95 ≤100 ms, `Max Memory Used` p95 ≤50 MB, and ARM64 ZIP ≤6,000,000 bytes.
  Effective cold-request duration is a separate required companion measurement with **no numeric target**.
- Its protocol measures a deployed ARM64 `provided.al2023` revision: record commit, configured memory,
  deployment timestamp and window; use the most recent slice of up to 30 days wholly inside one
  deployment interval; require ≥100 cold `REPORT` samples for INIT and ≥100 total `REPORT` samples for
  memory in that same slice. Sort numeric samples and select rank `ceil(0.95 × n)`; preserve query or
  samples. Measure the exact ZIP consumed by deployment. Insufficient samples mean **unverified**.
- [ROADMAP](../ROADMAP.md), lines 25–27 and 76–101, says deployed identity, AL2023 execution and
  production INIT/memory remain unverified. Optimization levers there are not authorized scope.
- The [accepted dated closeout](2026-10-16-delivery-and-diagnostics-closeout.md#master-ci-and-artifact),
  lines 111–133, reports master `df953f247aed5d03d89b4d1af6bb78e10214effb`, CI run
  `38903269306` / attempt `1`, artifact `12047446938`, inner ZIP **5,992,808 bytes**, SHA-256
  `0cda2b1f84e54ab54cbeeb100b78b3b3382815f4e6685ff936b82e4625d0e04f`.
  The separate GitHub archive was 5,968,184 bytes. ELF inspection found AArch64; it was not executed.
  This was check-time artifact eligibility, **not promotion**; no Lambda/stack deploy occurred in
  that closeout. Artifact availability, ancestry and eligibility have not been rechecked here.
- July's [performance baseline](2026-07-02-performance-opportunities.md), lines 10–37, is historical:
  approximately 40 sampled `REPORT` lines and a different percentile-era protocol do not establish
  present pass/fail. Neither those observations nor local fake-provider BDN results fill this gap.

## Proof chain and present unknowns

Each row needs its own evidence; a later row does not follow merely from the preceding row.

| Proof | Identity/evidence needed | Instrument or report already present | Unknown without later authorized reads |
| --- | --- | --- | --- |
| Hosted artifact → source | Repository, full source SHA, CI run/attempt/artifact ID, manifest, archive and inner-ZIP hashes/lengths | CI records manifest and selection summary; promotion validates them and master ancestry | Fresh availability/eligibility of any chosen artifact; which candidate Deniz selects |
| Artifact → deployed code | Same deployment-package hash, actual consumed ZIP length; promotion record linking selected artifact/source to deploy attempt | Workflow places verified ZIP beside verified-source CDK; dated hosted ZIP report only | Actual Lambda package identity; whether any current deployment consumed that ZIP |
| Stack → production function/route | Account/region, stack ID/resource mapping, Lambda ARN, API route/stage/integration and any qualifier | CDK source connects a function to HTTP API and CloudFront | Deployed template, out-of-band changes and effective route target |
| Configuration → actual execution | ARM64 + `provided.al2023` configuration for that code revision, correlated invocation/platform logs and successful output | ZIP bootstrap and request handler exist; bounded smoke is implemented | AL2023 compatibility and execution of the identified deployed package |
| Execution → INIT/memory goals | Isolated deployment slice, revision/configuration identity, complete eligible samples and exact nearest-rank calculation | Standard AWS platform logs are the canonical instrument; no production measurement collector in the inspected delivery path | Live log format/retention/access, counts, numeric results and compliance |
| Cold execution → effective latency | Cold request correlation, route/workload, Lambda-side timing and, if claimed, client-observed timing | Request-ID scope and method/path logging; smoke validates contracts, not latency distributions | Representative route cold samples, client/network boundary and comparison baseline |

Source instruments: [CI](../../.github/workflows/ci-cd.yml), lines 118–176;
[artifact helper](../../.github/scripts/delivery/artifact.mjs), lines 57–109, 166–177, 225–251;
[deploy workflow](../../.github/workflows/deploy.yml), lines 74–94 and 123–147.
The helper's SHA is hexadecimal (line 8); the archive digest and deployment-ZIP digest are distinct.
The deploy workflow does **not** read back Lambda `CodeSha256` or collect performance samples.

### What the runtime and smoke actually exercise

- [Function construct](../../build/BadgeSmith.CDK.Shared/Constructs/BadgeSmithFunctionConstruct.cs),
  lines 71–89 and 100–102: source intent is `provided.al2023`, ARM64, 512 MB, `bootstrap`, ZIP asset.
  [HTTP API construct](../../build/BadgeSmith.CDK.Shared/Constructs/BadgeSmithHttpApiConstruct.cs),
  lines 14–27, passes this function directly to the integration. These inspected constructs do not
  create a version or alias; this is **not** proof that live routing is unqualified or unchanged.
- [Program](../../src/BadgeSmith.Api/Program.cs), lines 10–19, creates router, processor and
  source-generated serializer, then runs `LambdaBootstrapBuilder`. The
  [registry](../../src/BadgeSmith.Api/Core/ApplicationRegistry.cs), lines 25–44, 94–113 and 167–196,
  leaves feature handlers/services and AWS clients lazy. First feature use can therefore do work
  after INIT; source does not establish how much time credentials, TLS or upstream calls consume.
- [Health](../../src/BadgeSmith.Api/Features/HealthCheck/HealthCheckHandler.cs), lines 18–29,
  returns a timestamped healthy response with no-store headers; it does not test DynamoDB, secrets
  or providers. [Processor](../../src/BadgeSmith.Api/Core/LambdaRequestProcessor.cs), lines 22–35,
  logs method/path under the Lambda request-ID scope; [console setup](../../src/BadgeSmith.Api/Core/Observability/LoggerFactory.cs),
  lines 51–57, includes scopes. Actual retained log shape/correlation still needs inspection.
- [Smoke](../../.github/scripts/delivery/smoke.mjs), lines 73–106 and 218–228, runs health, test
  badge, NuGet badge in order on Gateway, then public origin, and validates response contracts.
  Health can already warm an environment before a badge request; none is guaranteed cold.
  The helper returns a check count, not per-request timing or request-ID evidence. Existing smoke
  inputs/budgets/failure rules remain owned by the [production guide](../../build/BadgeSmith.CDK/README.md#required-get-smoke).
- [Cache canon](../architecture/runtime-and-routing.md#cache-layers), lines 19–41, separates edge,
  browser, conditional requests and provider validators. Direct Gateway bypasses CloudFront; public
  badge responses can be edge hits, including default error caching. A public success does not
  establish a new Lambda invocation or current Lambda code. Neither smoke mode proves all routes.

## Candidate collection sources — proposed only, none run

These are options for a later **authorized operator**, not permissions or commands to execute now.
Retrieval of existing data is distinct from sending requests that execute production code.

| Source | What it can establish | Limit / handling requirement |
| --- | --- | --- |
| Existing CI/deployment records and artifact manifest | Selected source/control-workflow identities, run/attempt/artifact, checked hashes/bytes, recorded promotion outcome | Recheck time-sensitive eligibility for a new promotion. Successful build is not a deploy; successful deploy summary is not Lambda hash readback. Source: local workflow/helper references above. |
| Lambda `GetFunctionConfiguration` [A1] | ARN, `Version`, `RevisionId`, `CodeSha256`, `CodeSize`, `LastModified`, runtime/architecture/memory, update/state and logging configuration | Allowlist output; response includes environment variables. Snapshot settings do not prove execution or full deployment history. |
| Lambda `GetFunction` [A2] | Configuration plus a deployment-package download link; a qualified read can address a published version | Link is valid for 10 minutes and must not enter durable artifacts. Download only if separately included in scope; inspect/hash/count without executing package. |
| CloudFormation `DescribeStacks`, resource mapping, `GetTemplate`, stack events [A3–A6] | Stack/output/resource identity, template code location and recorded update chronology | Restrict to `BadgeSmithStack`/its exact stack ID; paginate. Template is declared state, not a drift or execution proof; timestamps are not a source-commit field. |
| API Gateway v2 integration/route/stage reads; alias lookup if referenced [A7, A8, A25, A26] | Integration URI/qualifier, route target and selected stage/deployment; alias version and extra weighted version, if any | Resolve the configuration actually deployed to the route/stage used, not merely the latest API configuration or a function with the expected name. Do not invent aliases/versions or silently pool weighted traffic. |
| Existing CloudWatch platform + application logs [A9–A14] | Invocation request IDs, version context, reported timings/memory, errors and route correlation where retained | Validate actual schema, coverage and interval attribution; use only necessary fields, not arbitrary application payloads. Logging availability/retention is unknown. |
| `FilterLogEvents`, or a bounded Logs Insights query/export [A15–A17] | Existing timestamped events or selected numeric rows and query statistics | Pagination/result limits, terminal status, scan cost and duplicate boundaries matter. `StartQuery` creates stored query results but does not invoke Lambda or alter the application. |
| New direct Gateway/public GET smoke | At observation time, contracts and reachability for selected endpoints; Gateway can exercise Lambda/dependency paths | **Active production execution, not passive evidence retrieval.** Needs separate approval, real existing inputs and cost bounds. Public edge hits can hide origin behavior; no cold-start guarantee. |

### Lambda identity and byte-count limits

AWS explicitly defines `CodeSha256` as the deployment-package SHA-256 and `CodeSize` as its size
**in bytes** [A1]. Thus API-reported deployment-package bytes are available; it would be wrong to
claim the API exposes no byte count. For an identified ZIP function, retain `CodeSize` as an AWS
observation and cross-check it against exact bytes from the verified consumed package. It is not
GitHub's wrapper archive size, the uncompressed bootstrap length, or a fresh file measurement.
The official CLI example shows a base64-form `CodeSha256` [A18]; normalize hash encoding before
comparing with the helper's hexadecimal SHA-256. Hash agreement links package content, not execution.

No dedicated source-commit field appears in the documented function configuration [A1]. Here the
source's fixed function description contains no commit, and the CI manifest is outside the inner
ZIP [artifact helper, lines 162–177]. Reconstruct commit linkage through verified artifact/provenance
and deployment records; do not interpret `RevisionId`, `Version`, a CDK asset key or `LastModified`
as a Git commit. Identical bytes from multiple source commits would not uniquely identify one.

`LastModified` means the function was last updated, not necessarily the deploy cutover instant [A1].
`RevisionId` is an update/revision identifier, not an immutable published-version number.
Unqualified invocation implicitly uses mutable `$LATEST`; published versions lock code and most
configuration [A19]. Alias routing may include another weighted version [A8]. Current reads alone
cannot reconstruct every historical `$LATEST` value or route change. Stack events/deploy records
and log context must establish the selected interval; otherwise attribution remains unverified.
CloudFormation updates configuration and code sequentially, allowing invocations between them [A20];
exclude/identify a transition rather than treating an update-start timestamp as a clean cutover.

## Measurement caveats: provider facts versus repository protocol

1. **Memory is an environment high-water observation.** AWS says that when invocations share an
   execution environment, `Max Memory Used` reports the maximum across its invocations [A9].
   The repository deliberately takes p95 of **all REPORT samples**, not one sample per environment.
   That is an invocation-weighted distribution of reported high-water values, not per-request
   allocations, live managed heap or BDN B/op. Environment reuse correlates samples. This qualifies
   interpretation; it is not evidence that the prescribed calculation is defective. Preserve MB
   as reported and document any byte/unit conversion rather than silently changing the threshold.
2. **Cold REPORT and INIT_REPORT are different records.** The custom-runtime guide shows INIT on a
   cold `REPORT` [A10]. Lifecycle docs say successful on-demand init normally has no separate
   `INIT_REPORT`; init failures do, and provisioned-concurrency/SnapStart behavior differs [A11].
   The system-level mapping also distinguishes on-demand init events at DEBUG and failures at WARN
   [A12]. Do not require a separate INIT_REPORT as the only cold detector, or count it as a REPORT.
3. **Missing Init Duration is not numeric zero.** It can be an ordinary warm report, but field
   absence alone does not prove every invocation was warm or that logging/parsing is complete.
   `initDurationMs` is optional in the platform report schema [A14]. After invoke failures, AWS can
   perform a **suppressed init** inside the following invocation's reported Duration without an
   explicit additional CloudWatch INIT phase [A11]. Ordinary REPORT-only evidence cannot enumerate
   all such reinitialization time. Preserve unknowns/errors instead of manufacturing cold samples.
4. **Inspect the actual runtime, format and log level.** `provided.al2023` is OS-only AL2023 [A21],
   not managed `dotnet10`. AWS documents old/new text REPORT failure formats [A11], JSON platform
   events [A13, A14], and system log filtering: INFO includes successful reports; WARN omits them
   [A12]. A parser that expects only text, one field order, an INIT_START runtime ARN, or managed
   .NET application-log behavior can miss data. The initStart schema makes runtime version optional.
   Read logging settings and representative existing records before selecting extraction syntax.
5. **One revision/window, complete counts.** The ≥100 cold / ≥100 total and ≤30-day requirements are
   repository policy, not AWS statistical guarantees. Do not widen into an earlier deployment,
   pool different configurations, or select only successful/fast routes to reach the count.
   Record total reports, parsed values, cold reports, failures, missing fields and excluded records;
   distinguish event time from ingestion time [A24]. Retention gaps or uncertain historical identity
   limit completeness. Under 100 cold samples leaves INIT unverified even if memory is verifiable.
6. **Logs Insights pct is not documented as this exact nearest-rank algorithm.** AWS describes
   `pct(field,95)` semantically, without specifying rank/interpolation behavior on [A22]. Do not
   assert equivalence, or assert a confirmed mismatch. Retaining only that aggregate would not
   establish the mandated rank calculation; a complete numeric export allows an auditable sort and
   rank selection. Compute separate populations for INIT and memory, not p95 of mixed warm zeros.
7. **Exports can be incomplete without looking empty.** `FilterLogEvents` may return partial/empty
   pages while a next token remains [A15]; Insights returns partial results while Running [A16].
   Current docs describe paginated query-result ceilings [A16, A17]. Retain terminal status/counts,
   collect all pages or partition within the same interval, and deduplicate overlaps by stable
   record identity. A capped page or console preview is not the all-REPORT sample population.
8. **Effective cold-request time needs an explicit boundary.** INIT and invoke Duration are
   distinct AWS measurements [A9–A11]. A same-request `Init Duration + Duration` can be labeled a
   Lambda-side cold-cost companion, not total client latency or a complete suppressed-init census.
   A percentile of paired sums is not the sum of separate percentiles. Client-observed Gateway
   latency additionally includes network/Gateway effects; CloudFront additionally changes the cache
   path. Record route, status, origin, cold classification and correlation limits. Existing smoke
   has no latency export; production telemetry is compiled out by [Dockerfile](../../src/BadgeSmith.Api/Dockerfile),
   lines 29–35. Neither a health smoke nor a fast edge response supplies a representative cold p95.

**Deviation assessment:** no contradiction of the current numeric goals or REPORT-based normative
protocol was established by these reads. High-water semantics, suppressed-init visibility and
undocumented `pct` rank details are bounded methodological concerns, not authorization to replace
the protocol. If live evidence cannot support its required attribution/fields, stop that compliance
claim and report the limit. Changing sample populations, targets or accepted measurement sources
belongs to the orchestrator under [deviation protocol](../engineering/deviation-protocol.md), lines 24–44.
AWS's .NET AOT walkthrough discusses managed .NET 8 and AL2023 build requirements [A23]; it is not
fresh proof about this .NET 10 OS-only ZIP. The [Dockerfile](../../src/BadgeSmith.Api/Dockerfile), lines
7 and 45–60, has separate SDK build, AL2023 image and ZIP-export stages. Building the ZIP does not
execute the AL2023 image; no specific ABI failure is established here.

## Decisions for scoping, not discoverable facts or approved choices

| Decision branch | Factual consequence / unresolved scope |
| --- | --- |
| Observe existing deployment vs select a new candidate | Existing logs can establish only the revision actually serving then. A new candidate requires fresh artifact selection/linkage and separately approved promotion/deployment; the historical `df953f2` ZIP is not automatically selected. |
| Evidence-collection completion vs compliance completion | A bounded increment can finish with a truthful unknown/fail result. Requiring all targets to pass is a different objective and can imply later optimization work; no optimization is approved by this note. |
| Enough passive cold samples vs insufficient samples | Count is discoverable after authorized access. Waiting within the current interval, closing as unverified, or proposing a separately authorized experiment are distinct choices; do not relax the existing minimum or mix intervals. |
| Passive observation vs deliberate requests/deployments | Logs/config reads do not deliberately generate cold starts. GET smoke executes code and may warm environments or reach upstreams. Load, resets, configuration changes and deploys are separate operations, not evidence-read fallbacks. |
| Lambda-side companion vs end-user cold latency | Choose the reported boundary/workload and correlation needed; no numeric effective-latency threshold is currently set. Existing mixed traffic, health-only or edge-cached samples cannot silently stand in for chosen badge routes. |
| Operator, permissions and budget | Decide the authorized operator, narrowly scoped resources/actions, query scan/download budget, observation duration, request budget if any and stop conditions. No discovery of credentials or widening on denial follows. |
| Durable evidence and redaction | Decide record/export destination, allowed identifiers, raw-data retention and reviewer access. Preserve reproducible numeric samples, hashes, timestamps and attribution without URLs bearing credentials, environment values, arbitrary bodies or personal paths. |

A possible **evidence packet**, not an approved schema: source/artifact/deployment/control identities;
allowlisted Lambda/routing snapshots; boundary timestamps and window justification; query/export
provenance and hashes; sample counts, units, parsing/error exclusions, exact p95 ranks and values;
separate size/INIT/memory/latency conclusions with unknowns. Read APIs may return secrets or signed
links even when the desired fields are harmless [A1, A2]; projection/redaction must precede storage.
Raw request paths in application logs also need review before retention. No new policy, glossary
entry, ADR, roadmap status, guardrail proposal or automatic recovery action is created here.

## Verification and unrun live checks

Performed: local source/canon reads, clean starting `git status --short`, HEAD read, initial memory
retrieval (historical lead only), Context7 AWS documentation lookup and public official AWS reads.
Local citation targets were read; sources above are observations of the stated snapshot.
**Not run:** authenticated AWS/GitHub reads, artifact refresh/download, live IAM/OIDC/CDK verification,
deployed-template/configuration/route/log reads, production HTTP, workflow dispatch, synth/diff/deploy,
build/test/AOT publish/benchmark/load/cold generation, installs, source setup, commit or push.
No new production result, current eligibility judgment or performance compliance claim follows.

## Primary external references

Official AWS pages consulted on 2026-10-16; API descriptions describe capabilities, not this account.

- [A1 — GetFunctionConfiguration](https://docs.aws.amazon.com/lambda/latest/api/API_GetFunctionConfiguration.html)
- [A2 — GetFunction](https://docs.aws.amazon.com/lambda/latest/api/API_GetFunction.html)
- [A3 — DescribeStacks](https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_DescribeStacks.html)
- [A4 — DescribeStackResources](https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_DescribeStackResources.html)
- [A5 — GetTemplate](https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_GetTemplate.html)
- [A6 — DescribeStackEvents](https://docs.aws.amazon.com/AWSCloudFormation/latest/APIReference/API_DescribeStackEvents.html)
- [A7 — API Gateway v2 GetIntegrations](https://docs.aws.amazon.com/apigatewayv2/latest/api-reference/apis-apiid-integrations.html)
- [A8 — GetAlias](https://docs.aws.amazon.com/lambda/latest/api/API_GetAlias.html)
- [A9 — C# REPORT fields, including shared-environment memory semantics](https://docs.aws.amazon.com/lambda/latest/dg/csharp-logging.html#csharp-logging-output)
- [A10 — Custom runtime initialization and bootstrap](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-custom.html)
- [A11 — Execution lifecycle, INIT_REPORT, failure formats and suppressed init](https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtime-environment.html)
- [A12 — System event log-level mapping and custom runtime controls](https://docs.aws.amazon.com/lambda/latest/dg/monitoring-cloudwatchlogs-log-level.html)
- [A13 — System/application log format boundaries](https://docs.aws.amazon.com/lambda/latest/dg/monitoring-cloudwatchlogs-logformat.html)
- [A14 — Platform event/report schema and optional fields](https://docs.aws.amazon.com/lambda/latest/dg/telemetry-schema-reference.html)
- [A15 — FilterLogEvents pagination and event identity](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_FilterLogEvents.html)
- [A16 — GetQueryResults status and pagination](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_GetQueryResults.html)
- [A17 — StartQuery time range, limits and stored results](https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_StartQuery.html)
- [A18 — Official GetFunction CLI output example](https://docs.aws.amazon.com/cli/latest/reference/lambda/get-function.html#examples)
- [A19 — Versions and unqualified $LATEST invocation](https://docs.aws.amazon.com/lambda/latest/dg/configuration-versions.html)
- [A20 — CloudFormation Lambda update sequencing and code location](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/aws-resource-lambda-function.html)
- [A21 — OS-only runtimes and architecture requirements](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-provided.html)
- [A22 — Logs Insights stats percentile definition](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/CWL_QuerySyntax-Stats.html#CWL_QuerySyntax-stats-functions)
- [A23 — .NET Native AOT walkthrough, with its managed-.NET-8 scope](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html)
- [A24 — Discoverable fields: event time versus ingestion time](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/CWL_AnalyzeLogData-discoverable-fields.html)
- [A25 — API Gateway v2 GetRoutes and route targets](https://docs.aws.amazon.com/apigatewayv2/latest/api-reference/apis-apiid-routes.html)
- [A26 — API Gateway v2 GetStages and deployment identity](https://docs.aws.amazon.com/apigatewayv2/latest/api-reference/apis-apiid-stages.html)
