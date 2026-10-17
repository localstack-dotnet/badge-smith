# Production evidence — Phase A blocked on local authentication

**Evidence snapshot: 2026-10-16T20:28:46.220108Z.** Repository:
`github.com/localstack-dotnet/badge-smith`; local source
`1044b6d6e7160fc8a3dedcd3154337a4dfb4bd9f`.

## Outcome

**Collection status: BLOCKED.** The installed Python AWS SDK could not obtain usable existing
authentication for the initial STS identity probe. No STS service response was received. No stack,
function, routing or log metadata reads followed, and no production evidence was collected.
This is a local access-path observation, not an AWS permission verdict or a production defect.

The user explicitly requested execution of the approved
[collection procedure](../plans/2026-10-16-production-evidence-collection.md). That started bounded
Phase A only; the mandatory preflight-based Phase B budget checkpoint remains unpassed.

## Direct observations and checks

| Observation/check | Result and limit |
| --- | --- |
| Existing local tools | AWS CLI unavailable; Boto3 available. No installation or setup performed. Tool availability alone established no authentication. |
| Expected account/region | Committed production hosted-zone context identifies account `377140207735`, region `eu-central-1`; subject is `BadgeSmithStack`. This is source context, not a verified live caller or deployment. |
| Sanitization controls | A synthetic identity response retained only allowlisted fields; sentinel Environment/UserId values were excluded. An unexpected synthetic account produced a blocked result. No real secret values were used in these controls. |
| SDK operation | One `sts.GetCallerIdentity` SDK-level probe through existing standard authentication. Explicit official regional HTTPS endpoint; EC2 metadata credential lookup disabled in the probe process. Credential resolution was left to the SDK; no credential material was manually inspected or emitted. |
| Probe result | `status=BLOCKED`, `reason=existing_authentication_unavailable`, `serviceResponseReceived=false`. No raw exception text, principal identifier or credential material was retained. |
| Later reads | Stack/function/API/log reads NOT RUN after the identity prerequisite failed. No alternate profile/identity probing or access setup. |
| Production operations | Log events, Logs Insights, artifact/package downloads, production HTTP, invocation, workflow dispatch, deploy, load/cold generation, configuration/IAM changes NOT RUN. |

Executor-selected Phase A bounds were announced and recorded before the probe: existing authorized
authentication only; exact-stack-linked metadata; at most 20 API attempts total and two pages per
listing; metadata history lookback at most 30 days; no automatic retries; connection/read timeouts
3/5 seconds and an overall metadata attempt horizon of 120 seconds. These are bounds on this
attempt, not new repository policy or a Phase B budget. No diagnostic log-event sample was included.

## Production conclusions

| Claim | Verdict | Reason |
| --- | --- | --- |
| Deployed revision and effective routing | UNVERIFIED | No authenticated production identity or resource metadata obtained. |
| Artifact/source/deployment linkage and exact deployed ZIP size | UNVERIFIED | No deployed package observation or new provenance comparison. Historical hosted evidence was not substituted. |
| ARM64 provided.al2023 execution | UNVERIFIED | Neither runtime metadata nor correlated execution evidence collected. |
| INIT, memory and Lambda-side cold timing | UNVERIFIED | No REPORT population retrieved; sample counts and log coverage are unknown, not zero. |

No numeric baseline JSON or sample CSV is created: empty data would not represent an observed
population. The [scoping research](2026-10-16-production-evidence-scoping.md) remains source evidence,
not a fresh account observation or an alternative performance result.

## Operator handoff — bounded Phase A only

Use an **already-authorized** operator context, without creating access or changing IAM/trust.
The collector follows the procedure's Task 1 projection/bounds and Task 2 exact-resource rules.
If any read is denied or a bound is reached, report the affected fields as unavailable and stop
that path. Return only a sanitized export; do not send credentials or complete SDK/CLI responses.

1. Confirm the intended existing caller account and `eu-central-1`; stop on an unexpected account.
   Retain account/region, a nonpersonal operator label and UTC collection times, not credentials,
   personal profile paths, session identifiers or full caller output.
2. Read the exact `BadgeSmithStack` identity and resolve only its relevant physical Lambda and
   HTTP API resources. Retain relevant resource IDs/status/times; exclude parameters, resource
   properties, whole templates and arbitrary outputs. Observe the bounded pagination/history rules.
3. Read the resolved function's allowlisted configuration: ARN/qualifier, Version, RevisionId,
   PackageType, CodeSha256, CodeSize, LastModified, Runtime, Architectures, MemorySize, State,
   LastUpdateStatus and LoggingConfig. Exclude Environment and unrelated configuration.
4. Follow the exact production API/stage/route/integration mapping and any referenced qualifier or
   weighted alias. Distinguish deployed-stage evidence from latest API configuration; report any
   historical cutover or effective-route uncertainty rather than guessing.
5. Describe only the identified function's actual log group/streams within the metadata bound.
   Return available retention/format/level/coverage metadata and its limitations. Read no log events
   and start no query. Metadata alone does not establish numeric samples or complete log coverage.
6. Return the sanitized preflight fields, collection provenance, allowed-action/page counts,
   missing/denied fields and interval-confidence limits. Attribute operator observations separately
   from any later independently checked consistency/calculations.

Then return to the procedure's **Task 3 checkpoint**. No substantial query/export, package download,
active smoke or deployment follows automatically. The available metadata must first support a
concrete collection method and budget proposal; no scan-cost estimate is fabricated here.

## Resume state

The scope and Phase A attempt are recorded; Task 2 remains blocked pending an attributed operator
export or an explicitly restored existing collection path. Tasks 3–5 have not executed. The plan
remains available for resumption; this limited blocked-attempt report does not close the increment,
authorize Phase B, or claim that any production goal passes or fails. No application code, package,
workflow, infrastructure or repository policy changed, and no commit/push was performed.
