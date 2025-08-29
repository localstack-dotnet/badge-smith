# BadgeSmith Lambda Performance Testing

Comprehensive k6 performance testing for the BadgeSmith Lambda function with realistic traffic patterns.

## 🚀 Quick Start

### Install k6

```bash
# Windows (Chocolatey)
choco install k6

# macOS (Homebrew)  
brew install k6

# Linux (apt)
sudo apt update && sudo apt install k6

# Or download from: https://k6.io/docs/getting-started/installation/
```

## 📊 Test Types

### 1. Quick Smoke Test (2 minutes)

Fast validation test to ensure the Lambda is responding correctly:

```bash
k6 run --duration 2m --vus 10 scripts/k6-perf-test.js
```

### 2. Standard Load Test (5 minutes)

Default test with moderate load - good for regular performance checks:

```bash
k6 run --duration 5m --vus 50 scripts/k6-perf-test.js
```

### 3. Stress Test (10 minutes)

High load test to find the breaking point and trigger memory pressure:

```bash
k6 run --duration 10m --vus 200 scripts/k6-perf-test.js
```

### 4. Endurance Test (30+ minutes)

Long-running test to detect memory leaks and performance degradation over time:

```bash
# 30 minute endurance test with sustained load
k6 run --duration 30m --vus 30 scripts/k6-perf-test.js

# 1 hour endurance test  
k6 run --duration 1h --vus 25 scripts/k6-perf-test.js

# 2 hour marathon test
k6 run --duration 2h --vus 20 scripts/k6-perf-test.js
```

### 5. Spike Test (3 minutes)

Short bursts of extreme load to test Lambda cold start handling:

```bash
k6 run --duration 3m --vus 500 scripts/k6-perf-test.js
```

### 6. Capacity Test (15 minutes)

Find maximum sustainable throughput:

```bash
k6 run --duration 15m --vus 300 scripts/k6-perf-test.js
```

## 🔧 Advanced Configuration

### Custom Staging Patterns

Override the built-in stages with your own load pattern:

```bash
# Gradual ramp-up test
k6 run --stage 1m:10,5m:50,5m:100,5m:150,1m:0 scripts/k6-perf-test.js

# Step load test
k6 run --stage 2m:25,2m:50,2m:75,2m:100,2m:0 scripts/k6-perf-test.js

# Spike pattern
k6 run --stage 30s:10,30s:500,1m:10,30s:800,1m:0 scripts/k6-perf-test.js
```

### Save Results to Files

```bash
# JSON output for detailed analysis
k6 run --duration 5m --vus 50 --out json=results.json scripts/k6-perf-test.js

# CSV output for spreadsheet analysis  
k6 run --duration 5m --vus 50 --out csv=results.csv scripts/k6-perf-test.js

# Multiple outputs
k6 run --duration 5m --vus 50 --out json=results.json --out csv=results.csv scripts/k6-perf-test.js
```

### Environment Variables

```bash
# Set custom API endpoint
K6_API_URL=https://your-api-gateway-url.amazonaws.com k6 run scripts/k6-perf-test.js

# Custom test duration and VUs
K6_DURATION=10m K6_VUS=100 k6 run scripts/k6-perf-test.js
```

## 📈 Understanding Results

k6 provides comprehensive reporting at the end of each test:

```text
✓ http_req_duration..............: avg=77ms  p(95)=66ms  p(99)=76ms
✓ http_req_failed................: 1.00%    ✓ 1700     ✗ 18  
✓ http_reqs......................: 1814     29.4/s
✓ cold_starts....................: 0.07%    ✓ 1429     ✗ 1
✓ cache_hits.....................: 85.5%    ✓ 1550     ✗ 264
✓ memory_pressure_responses......: 0        count
✓ All thresholds passed!
```

### Key Metrics

- **http_req_duration**: Response times (avg, p95, p99) - aim for p95 < 200ms
- **http_req_failed**: Error rate percentage - aim for < 5%
- **http_reqs**: Total requests and requests/second
- **cold_starts**: Cold start detection rate - aim for < 5%
- **cache_hits**: Cache effectiveness - higher is better
- **memory_pressure_responses**: Lambda memory issues - should be 0

### Performance Targets

| Metric | Excellent | Good | Needs Work |
|--------|-----------|------|------------|
| P95 Response Time | < 100ms | < 200ms | > 500ms |
| Error Rate | < 1% | < 5% | > 10% |
| Cold Start Rate | < 1% | < 5% | > 10% |
| Cache Hit Rate | > 80% | > 60% | < 40% |

## 🎯 Test Scenarios

The test automatically simulates realistic traffic patterns:

- **40% NuGet Package Badges** (`/badges/packages/nuget/{package}`)
  - Real packages: Newtonsoft.Json, Microsoft.Extensions.Http, etc.
  
- **30% GitHub Package Badges** (`/badges/packages/github/{org}/{package}`)
  - Real orgs: microsoft, facebook, localstack-dotnet
  
- **15% Test Result Badges** (`/badges/tests/{platform}/{owner}/{repo}/{branch}`)
  - Multiple platforms: linux, windows
  
- **10% Health Checks & Redirects** (`/health`, redirects)
  - Administrative endpoints
  
- **5% Edge Cases** (URL encoding, invalid routes, rapid requests)
  - Error handling and cache testing

## 🔍 Live Monitoring

During tests, watch for:

```text
📊 Progress: 1340 requests completed | VUs: 30 | Time: 270s
⚠️  Slow NuGet response: 245ms for Microsoft.Extensions.Http  
🐙 Slow GitHub response: 650ms for microsoft/vscode
```

## ☁️ AWS Integration

Monitor these AWS Lambda metrics during tests:

1. **Lambda Console**: Functions → badge-smith-function → Monitoring
2. **CloudWatch Metrics**:
   - Duration, Memory usage, Concurrent executions
   - Throttles, Errors, Dead letter queue
3. **Cost Tracking**: Monitor billing during high-load tests

## 🛠️ Troubleshooting

### High Error Rates

- Check Lambda logs in CloudWatch
- Verify API Gateway URL is correct
- Review external API connectivity (GitHub, NuGet)

### Poor Performance

- Increase Lambda memory allocation (current: 512MB)
- Check for cold starts during load spikes
- Analyze memory usage patterns

### Failed Thresholds

k6 will show exactly which thresholds failed:

```text
✗ http_req_duration..............: avg=200ms p(95)=500ms p(99)=1s
✗ cold_starts....................: 10.00%
✗ Some thresholds failed!
```

## 🔄 CI/CD Integration

Add performance testing to your pipeline:

```yaml
# GitHub Actions example
- name: Performance Test
  run: k6 run --duration 2m --vus 20 scripts/k6-perf-test.js
```

```yaml
# Azure DevOps example  
- script: k6 run --duration 5m --vus 50 --out json=perf-results.json scripts/k6-perf-test.js
  displayName: 'Run Performance Tests'
```
