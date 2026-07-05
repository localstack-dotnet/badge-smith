# LocalStack Lambda Image Spike

Date: 2026-07-04

## Purpose

Determine whether BadgeSmith's published Native AOT Lambda container image can be executed locally through LocalStack over a normal HTTP endpoint for k6 benchmark runs.

## Environment

| Component | Observed value |
| --- | --- |
| OS shell | Windows PowerShell with Docker Desktop Linux backend |
| Docker | Client 29.5.3, Server 29.5.3 |
| AWS CLI | aws-cli/2.34.37 Python/3.14.4 Windows/11 exe/AMD64 |
| curl | 8.20.0 |
| LocalStack image | `localstack/localstack:4.6` |
| LocalStack edition/version | Community, 4.6.0 |
| Lambda image | `badge-smith:localstack-spike` |
| Lambda image size | 193 MB |

Full command excerpts are in `artifacts/localstack-lambda-image-spike.log`.

## Commands And Evidence

### Image Build

The Lambda image built successfully from `src/BadgeSmith.Api/Dockerfile`:

```text
docker build -f "src/BadgeSmith.Api/Dockerfile" --target lambda-image -t badge-smith:localstack-spike .
...
BadgeSmith.Api -> /artifacts/publish/
naming to docker.io/library/badge-smith:localstack-spike done
```

### LocalStack Startup

LocalStack started with Docker socket access and reported healthy:

```text
docker run -d --name bs-ls-spike -p 4566:4566 -e DEBUG=1 -v /var/run/docker.sock:/var/run/docker.sock localstack/localstack:4.6

Invoke-WebRequest http://localhost:4566/_localstack/health
{"edition":"community","version":"4.6.0",...}
```

### Seeding

The planned command did not seed the spike container:

```text
bash scripts/perf-baseline-seed.sh bridge
aws --endpoint-url http://localhost:4566 dynamodb list-tables
TableNames: []
```

Tracing showed the script is hard-coded around `bs-perf-ls` local endpoint discovery, while this spike uses `bs-ls-spike`:

```text
+ docker port bs-perf-ls 4566/tcp
+ LS_PORT=
```

The same DynamoDB tables, Secrets Manager entries, org-secret mappings, and five benchmark test-result rows were seeded manually through `aws --endpoint-url http://localhost:4566` with dummy LocalStack credentials. Verification:

```text
badge-smith-github-org-secrets badge-smith-hmac-nonce badge-smith-test-result
badgesmith/github/test-org/testdata badgesmith/github/test-org/package badgesmith/github/localstack-dotnet/package
5
```

### Function URL Attempt

LocalStack accepted the image function definition and Function URL configuration:

```text
aws --endpoint-url http://localhost:4566 lambda create-function --function-name badge-smith-spike --package-type Image --code ImageUri=badge-smith:localstack-spike ...
FunctionName: badge-smith-spike
PackageType: Image
Architectures: x86_64

aws --endpoint-url http://localhost:4566 lambda wait function-active-v2 --function-name badge-smith-spike

aws --endpoint-url http://localhost:4566 lambda create-function-url-config --function-name badge-smith-spike --auth-type NONE
FunctionUrl: http://i8eh5cigasvweb5v15xh8by1pzh4wn8t.lambda-url.us-east-1.localhost.localstack.cloud:4566/
```

Invoking `/health` through the Function URL failed before BadgeSmith started:

```text
curl -i "http://i8eh5cigasvweb5v15xh8by1pzh4wn8t.lambda-url.us-east-1.localhost.localstack.cloud:4566/health"
HTTP/1.1 500 INTERNAL SERVER ERROR
X-Amzn-Errortype: InternalError

NotImplementedError: Container images are a Pro feature.
localstack.services.lambda_.invocation.assignment.AssignmentException: Could not start new environment: NotImplementedError:Container images are a Pro feature.
```

API Gateway v2 was not attempted because the failure is at Lambda image execution startup. API Gateway v2 would still invoke the same image-based Lambda and cannot prove HTTP event shape while LocalStack Community refuses to run the container image.

## Decision

- Selected target: none
- Reason: LocalStack failed to execute the published BadgeSmith Lambda image reliably. Do not reintroduce RIE; use Aspire Testing for contract coverage and deployed AWS for AOT artifact verification.

## Follow-Up

- Task 8 cannot build a working LocalStack image-backed benchmark harness in LocalStack Community from this evidence.
- `scripts/perf-baseline-seed.sh` should be updated or guarded because it assumes the benchmark container name `bs-perf-ls` and did not seed the Task 7 `bs-ls-spike` endpoint.
