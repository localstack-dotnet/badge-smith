# BadgeSmith.CDK.Tests

This project owns focused CloudFormation assertions for BadgeSmith's production CloudFront cache,
transport, and Lambda runtime contracts.

The tests synthesize constructs in process through `Amazon.CDK.Assertions`. They require Node.js on
`PATH` for the JSII kernel, but they do not require Docker, AWS credentials, or the AWS CDK CLI.
Test parallelization is disabled so the suite uses one JSII kernel at a time.

Run from the repository root:

```bash
dotnet test tests/BadgeSmith.CDK.Tests/BadgeSmith.CDK.Tests.csproj -c Release
```
