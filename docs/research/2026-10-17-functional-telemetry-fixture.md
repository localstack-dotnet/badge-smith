# Functional-suite stall — test-owned telemetry receiver

**Resolved locally: the complete API test project passed twice, 579 passed / 0 failed / 0 skipped.**
The response-allocation candidate is unchanged. This follow-up fixes its test environment, not
production Lambda startup or memory. No deployment, package/build-policy change or test timeout
increase was made.

## Cause and controlled comparison

The ordinary development build enables OpenTelemetry and wraps requests with `AWSLambdaWrapper`.
The pinned wrapper stops the activity and synchronously calls `TracerProvider.ForceFlush()` before
returning. Aspire Testing disables its dashboard while retaining a default OTLP URL of
`http://localhost:4317`; there was no listener on that port during diagnosis.

The existing fixture provided fake package/AWS services but no telemetry receiver. Its requests
therefore depended on an unavailable exporter destination. A narrowed four-test routing run passed,
but repeated requests spent seconds waiting for response headers, not reading response bodies.
Temporary diagnostics retained only stage labels, numeric statuses and timestamps—no raw resource
logs, request bodies, headers or export payloads.

| Sequential routing request | Existing telemetry destination | Test-owned receiver |
| --- | ---: | ---: |
| GET health | 1,914 ms | 955 ms |
| HEAD health | 9,337 ms | 72 ms |
| OPTIONS | 6,402 ms | 103 ms |
| Unknown route | 8,503 ms | 104 ms |

Both runs used the same application code and short disk-backed temporary directory. The controlled
change supplied a local HTTP/protobuf OTLP receiver; telemetry, retries and Lambda flushing stayed
enabled. All four HTTP assertions passed in both runs; the receiver observed four exports in the
second. This is test-harness latency, not a Lambda performance benchmark.

The earlier individual 60-second timeout failures did not retain flush-level traces, so they cannot
each be retrospectively attributed with the same certainty. The reproducible export dependency and
two complete post-fix suite runs resolve the current validation blocker.

## Minimal fixture fix

- Reuse the fixture-owned WireMock server to acknowledge OTLP protobuf exports with HTTP 200 and
  an empty protobuf export response. The mapping accepts only POSTs at the fixture's OTLP paths
  with the protobuf content type.
- Point only the test model's `LambdaProjectResource` exporter at that receiver and select
  `http/protobuf`. Do not change the real AppHost, application telemetry, production compilation
  flags, retries, routes or cache behavior.
- Add `TelemetryContractTests`: complete two real health requests and require at least two new
  matched, HTTP-200 export events at the fixture-owned stub. Counting only matched successful
  events prevents an unmatched/404 receiver from satisfying the assertion.
- Preserve every existing test and its timeouts. No wall-clock assertion was added.

This receiver acknowledges and discards test telemetry; it does not validate the semantic contents
of OTLP payloads or replace an observability integration test.

## Verification

1. The new regression test failed against the old fixture: **0 successful exports** after both
   requests completed. The final matched-stub observer was separately run red before the fix.
2. Fixed fixture: **5/5** targeted routing/telemetry tests passed; subsequent routing calls were
   approximately 68–109 ms.
3. Unfiltered Release API project: **579/579**, no failures/skips, then **579/579** again using a
   fresh private disk-backed temporary directory. Both remained inside the original 240-second
   orchestration bound, without changing the client's 60-second timeout or fixture startup limit.
4. Original performance project Release build: **0 warnings / 0 errors**. Slopwatch: **0 issues**.
5. Temporary diagnostic class, environment hooks and client instrumentation were removed. The
   existing `ContractHttpClient.cs` returned byte-for-byte to its pre-diagnosis contents.
6. Native AOT was not republished: this follow-up changes only test code/data. Production source,
   including the [measured response-helper candidate](2026-10-17-response-allocation-optimization.md),
   is unchanged, so its retained local A/B observations are not replaced or reinterpreted.

The [reduced evidence](baselines/2026-10-17-functional-telemetry-fixture.json) retains timings,
test counters, source hashes and exact version/source identities. Raw test logs and transient
diagnostics remain in persistent local scratch, not `/tmp` or committed application logs.

## Compatibility evidence

Matching `external/` checkouts were absent; targeted upstream reads were used, not default branches.

- **Aspire.Hosting / Testing 13.4.6:** package repository commit
  [`87fe259e4fc244c599019a7b1304c85a1488f248`](https://github.com/microsoft/aspire/tree/87fe259e4fc244c599019a7b1304c85a1488f248).
  `DistributedApplicationFactory.ConfigureBuilder` disables the dashboard and supplies port 4317
  as the default OTLP URL; `ResourceLoggerService` supplied diagnostic log snapshots.
- **Aspire.Hosting.AWS 13.3.1:** release `release_2026-06-19`, resolved to
  [`b8f5c040b2a41a655ab9ae4d2c528803d5066ae6`](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/tree/b8f5c040b2a41a655ab9ae4d2c528803d5066ae6).
  `LambdaExtensions.AddAWSLambdaFunction` calls `WithOpenTelemetry`, including `WithOtlpExporter`.
- **OpenTelemetry.Instrumentation.AWSLambda 1.16.0:** package repository commit
  [`e88c07f7d6a0bd8c09346b75cf951d61222e76bb`](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/e88c07f7d6a0bd8c09346b75cf951d61222e76bb).
  `AWSLambdaWrapper.OnFunctionStop` calls `ForceFlush`.
- **WireMock 3.9.1:** the existing fixture image and admin API were retained; successful serve events
  are filtered by the OTLP stub ID and response status. No dependency was added.
- LocalStack hosting/client wiring was not changed; this is not a new compatibility claim about it.

Production INIT/RSS/ARM64 evidence, independent review, commit approval and deployment remain separate
from these completed local checks.
