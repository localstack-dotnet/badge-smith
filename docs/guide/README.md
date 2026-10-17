# BadgeSmith consumer guide

Start with the kind of badge you want to display, then connect test publication if needed.
These pages describe the existing service; the [public README](../../README.md) carries live
examples, the product story and first-run paths.

| Read | Use it to |
| --- | --- |
| [Package badges](package-badges.md) | Display a NuGet.org or GitHub Packages version and select a version range or include prereleases |
| [Test results](test-results.md) | Display a test badge, link its report, and publish results through the supported action or CLI |
| [Domain glossary](../../GLOSSARY.md) | Distinguish package providers, test results, report destinations and workflow references |

## Operating your own deployment

- [Tooling reference](../../tools/README.md): command invocation, payload submission and organization secret setup.
- [Production CDK guide](../../build/BadgeSmith.CDK/README.md): production artifacts, stack selection and approval-gated operations.
- [Local development](../engineering/local-development.md#apphost-and-contract-tests): AppHost and deterministic contract-test setup.
- [Local-performance CDK guide](../../build/BadgeSmith.CDK.LocalPerformance/README.md): the separate LocalStack-only app, never an AWS deployment target.

For implementation work, start with [AGENTS.md](../../AGENTS.md). Documentation ownership and
lifecycle belong to the [documentation policy](../engineering/documentation.md), not this consumer path.
