# BadgeSmith Test Ingestion Scripts

Scripts for testing HMAC authentication and test result ingestion endpoints.

## 🚀 Quick Start

### Prerequisites

**PowerShell (Windows/Linux/macOS):**

- PowerShell 7.0+

**Bash (Linux/macOS/WSL):**

- bash 4.0+
- curl
- openssl
- jq (optional, for pretty JSON output)
- uuidgen

### 1. Start BadgeSmith Locally

```bash
# Start Aspire with LocalStack
dotnet run --project src/BadgeSmith.Host
```

### 2. Set Up Test Secret

Make sure you have a test organization configured in your `tests/seeders/BadgeSmith.DynamoDb.Seeders/organization-pat-mapping.json`:

```json
{
  "secrets": [
    {
      "org_name": "localstack-dotnet",
      "name": "test-secret-name",
      "secret": "your-test-hmac-secret-here",
      "type": "TestData",
      "description": "Test HMAC secret for ingestion"
    }
  ]
}
```

### 3. Test the Ingestion Endpoint

**PowerShell:**

```powershell
# Using sample payload file
.\scripts\test-ingestion.ps1 -BaseUrl "http://localhost:9474" `
  -Owner "localstack-dotnet" -Repo "localstack.client" `
  -Platform "linux" -Branch "main" -Secret "your-test-hmac-secret-here" `
  -PayloadFile "scripts\sample-test-payload.json" -ShowDetails

# Using inline payload
.\scripts\test-ingestion.ps1 -BaseUrl "http://localhost:9474" `
  -Owner "localstack-dotnet" -Repo "localstack.client" `
  -Platform "linux" -Branch "main" -Secret "your-test-hmac-secret-here" `
  -Payload '{"platform":"Linux","passed":190,"failed":0,"skipped":0,"total":190,"url_html":"https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/runs/47628811004","timestamp":"2025-09-05T10:57:00Z","commit":"4d8474bda0b16fbbb69887d0d08c3885843bbdc7","run_id":"16814735762","workflow_run_url":"https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/runs/16814735762"}'
```

**Bash:**

```bash
# Using sample payload file
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "your-test-hmac-secret-here" \
  --payload-file "scripts/sample-test-payload.json" --verbose

# Using inline payload
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "your-test-hmac-secret-here" \
  --payload '{"platform":"Linux","passed":190,"failed":0,"skipped":0,"total":190,"url_html":"https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/runs/47628811004","timestamp":"2025-09-05T10:57:00Z","commit":"4d8474bda0b16fbbb69887d0d08c3885843bbdc7","run_id":"16814735762","workflow_run_url":"https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/runs/16814735762"}'
```

## 📊 Expected Responses

### Success (201 Created)

```json
{
  "test_result_id": "badge-smith-abc123...",
  "repository": "localstack-dotnet/localstack.client/linux/main",
  "timestamp": "2025-09-05T10:57:00.123Z"
}
```

### Authentication Errors (400/401)

```json
{
  "message": "X-Signature header is required",
  "error_details": [
    {
      "error_code": "MISSING_AUTH_HEADERS",
      "property_name": "headers"
    }
  ]
}
```

### Validation Errors (400)

```json
{
  "message": "Test counts cannot be negative",
  "error_details": [
    {
      "error_code": "INVALID_TEST_PAYLOAD",
      "property_name": "payload"
    }
  ]
}
```

### Duplicate Results (409 Conflict)

```json
{
  "message": "Test result with run_id '16814735762' already exists",
  "error_details": [
    {
      "error_code": "DUPLICATE_TEST_RESULT",
      "property_name": "run_id"
    }
  ]
}
```

## 🔐 HMAC Authentication Details

The scripts automatically handle:

1. **Signature Generation**: HMAC-SHA256 of the exact payload
2. **Timestamp**: ISO 8601 format with current UTC time
3. **Nonce**: Unique GUID for replay protection
4. **Headers**: Proper X-Signature, X-Timestamp, X-Nonce format

### Security Notes

- ⚠️ **Secret Management**: Never commit real secrets to version control
- 🔒 **Payload Integrity**: The exact JSON string is hashed - formatting matters
- ⏰ **Timestamp Window**: Requests are valid for 5 minutes (configurable)
- 🔄 **Nonce Uniqueness**: Each request needs a unique nonce

## 🧪 Testing Scenarios

### Valid Request

```bash
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "test-secret" \
  --payload-file "scripts/sample-test-payload.json" --verbose
```

### Invalid Signature (should fail with 400)

```bash
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "wrong-secret" \
  --payload-file "scripts/sample-test-payload.json"
```

### Duplicate Run ID (should fail with 409 after first success)

```bash
# Run the same request twice - second should fail with 409
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "test-secret" \
  --payload-file "scripts/sample-test-payload.json"
```

### Invalid Payload (should fail with 400)

```bash
./scripts/test-ingestion.sh --base-url "http://localhost:9474" \
  --owner "localstack-dotnet" --repo "localstack.client" \
  --platform "linux" --branch "main" --secret "test-secret" \
  --payload '{"platform":"Linux","passed":-1,"total":0}'
```
