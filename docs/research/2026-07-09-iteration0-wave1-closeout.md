# Iteration 0 And Wave 1 Closeout Evidence

Date: 2026-07-09

Historical evidence for the Iteration 0 local benchmark boundary and the Wave 1 platform refresh.
Current behavior, commands, and package versions live in [`ARCHITECTURE.md`](../../ARCHITECTURE.md),
[`tools/README.md`](../../tools/README.md), the CDK app guides,
[`docs/ROADMAP.md`](../ROADMAP.md), and [`Directory.Packages.props`](../../Directory.Packages.props).

## Iteration 0 Local Benchmark Boundary

Iteration 0 closed in `991769e` on 2026-07-05. The LocalStack Community 4.6 workflow did not deploy
API Gateway v2 resources through CloudFormation, so the local-performance CDK stack exposed a Lambda
Function URL fallback while production retained API Gateway HTTP v2. This was an observation about
that version and workflow, not a standing compatibility claim for later LocalStack releases.

At closeout, the local benchmark scripts orchestrated LocalStack seeding and k6. Wave 1.7 later
retired that orchestration rather than half-porting it to the file-based CLI. The k6 scenario remains
a direct HTTP harness; a future `badgesmith perf baseline` command owns any renewed seed-and-run
orchestration.

## Wave 1.7 Platform Snapshot

The roadmap closeout recorded in `ce1ecfd` included Aspire 13.4.6,
LocalStack.Aspire.Hosting 13.4.0, an explicit Aspire.Hosting.AWS 13.3.1 dependency, a stable central
package refresh, and MessagePack removal. Those versions describe the 2026-07-09 closeout snapshot;
`Directory.Packages.props` owns the current package set.
