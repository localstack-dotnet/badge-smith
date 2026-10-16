# Delivery CDK diff semantics — 2026-10-16 evidence

This is dated source evidence for the delivery increment based on BadgeSmith
`d778400bd16cee44994f1c9bb68121e6a54d66cf`. It is not the current CLI version
inventory. The [deployment workflow](../../.github/workflows/deploy.yml) owns
current pins; the [production guide](../../build/BadgeSmith.CDK/README.md) owns
operational rules. Revalidate these semantics after a CLI version change.

## Package identity and method

The author and a bounded read-only helper inspected an existing cached npm tarball
offline, without installing packages, executing CDK, or making AWS calls. The author
re-read the relevant source and checked SHA-512 integrity against the cache index.
The independent R1 reviewer later spot-checked the tarball SHA-256 and the principal
source branches. These observations do not establish hosted behavior or permissions.

- Package: `aws-cdk`, version `2.1135.1`.
- Registry artifact: <https://registry.npmjs.org/aws-cdk/-/aws-cdk-2.1135.1.tgz>.
- Source repository: <https://github.com/aws/aws-cdk-cli>, `packages/aws-cdk`.
- Tarball size: **6,824,929 bytes**.
- SHA-256: `181b2d36df5681593e86b6015a77dd65b639faa1df093b3b0d492af5e0cd083a`.
- Verified cache integrity:
  `sha512-g1jcMfWlyYtGamFJ/kPBOCuchl3NfwTF2UwOLTIDN0nJbGm84EAO+c8DlYnaemM8UmKFkdoq4BGdmiNL5nHWwA==`.

No personal cache paths are needed to identify this package. Source locations below
are relative to `package/` inside this exact tarball; they may change in another version.

## Differences versus errors

The inspected command was:

```bash
cdk diff BadgeSmithStack --no-fail --method change-set
```

| Package source | Evidence |
| --- | --- |
| `lib/cli/parse-command-line-arguments.js:924–927` | Declares the boolean `fail` option. |
| `lib/cli/parse-command-line-arguments.js:947–953` | Supports `auto`, `change-set`, and `template`; documents automatic template fallback. |
| `lib/index.js:311254,311263,311378–311380` | Boolean negation is enabled with `no-`; `--no-fail` assigns false. |
| `lib/cli/cli.js:349` | Preserves an explicit `fail=false`. |
| `lib/cli/cli.js:710–713` | Preserves the explicit non-auto method. |
| `lib/cli/cdk-toolkit.js:344` | Returns `diffs && options.fail ? 1 : 0`; differences succeed with `--no-fail`. |
| `lib/cli/cdk-toolkit.js:354–378` | Checks stack access; explicit change-set rethrows at `360–362`; auto falls back at `364–368`; `378` passes `failOnError=true` for the explicit method. |
| `lib/index.js:94740–94748` | Explicit method rethrows change-set creation errors; auto permits template fallback. |
| `lib/cli/cli.js:858–869` | Propagated errors reach a final handler that sets `process.exitCode = 1`. |

`--no-fail` suppresses the difference exit status, not genuine errors. Default
`auto` still catches some errors internally, which is why it is insufficient for
the approved strict error-propagation objective. Explicit `template` skips change-set
creation (`lib/cli/cdk-toolkit.js:309–311`) and cannot prove it would succeed.

## Missing stacks, denials, and side effects

- `lib/index.js:92595–92604`: `CloudFormationStack.lookup` converts only the exact
  `ValidationError` message `Stack with id ${stackName} does not exist` into a missing
  stack wrapper. Actual `DescribeStacks` AccessDenied and other errors rethrow.
- `lib/index.js:92636–92645`: `.template()` returns `{}` only for a missing stack;
  otherwise `GetTemplate` errors propagate.
- `lib/index.js:110086–110089,92745–92763`: the current-template lookup chain adds
  no broad catch around stack lookup/template reads.
- `lib/index.js:110342–110350`: `Deployments.stackExists` awaits lookup and returns
  `.exists`. `lib/cli/cdk-toolkit.js:352–358` intentionally discards the boolean;
  a missing stack is not a reason to skip the diff guard.
- `lib/index.js:94776–94786,94811–94825`: missing stacks select a **CREATE** change set.
  `94836–94847` cleans up the change set and, for a missing stack, the empty placeholder.
- `lib/index.js:109922–109931`: the CLI's intrinsic best-effort lookup-role setup
  can warn and try existing stack-operation credentials. Do not claim every
  role-assumption denial immediately aborts; actual stack-read/change-set errors propagate.
- R1 reviewer spot-check, `lib/index.js:94110–94116,94316`: a failed change set with
  the recognized no-changes reason is classified as `Diagnosis.noProblem()`. An
  unchanged infrastructure diff is not a genuine failure. This is static evidence.

This method is **not inherently AWS read-only**. It may publish template assets
(`lib/index.js:94766–94767,94788–94801`), create/delete change sets, and create/delete
an empty placeholder for a new stack. Existing permissions must suffice. No IAM
expansion or template fallback was authorized or added for insufficient permissions.

## Validation boundaries

Local shell fixtures run the workflow's extracted diff/deploy commands under
`bash -e` with a `cdk` stand-in: zero diff status permits deploy for differences
and the missing-stack case; status 42 for stack-access failure and 43 for change-set
failure prevent deploy. The guard runs in each case. These fixtures establish
workflow propagation, not actual AWS behavior.

Hosted workflow execution, live CDK/AWS behavior, role sufficiency, production
environment branch restrictions, and repository/environment/audience OIDC trust
remain unverified. No AWS read or mutation was performed to produce this evidence.
