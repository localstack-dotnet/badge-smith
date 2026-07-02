// BadgeSmith k6 Performance Test
// Tests Lambda resilience, memory allocation, and cold start patterns
// Run with: k6 run --duration 5m --vus 50 scripts/k6-perf-test.js

import http from "k6/http";
import { check, group, sleep } from "k6";
import { Rate, Trend, Counter } from "k6/metrics";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";

// Custom metrics for detailed analysis
const coldStartRate = new Rate("cold_starts");
const responseTimeP95 = new Trend("response_time_p95");
const errorRate = new Rate("errors");
const memoryPressureCounter = new Counter("memory_pressure_responses");
const cacheHitRate = new Rate("cache_hits");

// Environment configuration
const BASE_URL = __ENV.K6_API_URL || "https://g4yecfi5hl.execute-api.eu-central-1.amazonaws.com";
const TARGET_MODE = __ENV.K6_TARGET_MODE || "http"; // http | rie
if (TARGET_MODE !== "http" && TARGET_MODE !== "rie") {
  throw new Error(`Invalid K6_TARGET_MODE: "${TARGET_MODE}". Must be "http" or "rie".`);
}
const DURATION = __ENV.K6_DURATION || null;
const VUS = __ENV.K6_VUS ? parseInt(__ENV.K6_VUS, 10) : null;

// Request wrapper routing all calls through HTTP or RIE mode
function invoke(method, path, headers, params) {
  const k6Params = Object.assign({}, params || {});
  if (headers && Object.keys(headers).length > 0) {
    k6Params.headers = headers;
  }

  if (TARGET_MODE === "http") {
    return http.request(method, `${BASE_URL}${path}`, null, k6Params);
  }

  // RIE mode: wrap into an API Gateway v2 event
  const qIndex = path.indexOf("?");
  const rawPath = qIndex >= 0 ? path.substring(0, qIndex) : path;
  const rawQueryString = qIndex >= 0 ? path.substring(qIndex + 1) : "";

  const queryStringParameters = {};
  if (rawQueryString) {
    rawQueryString.split("&").forEach(function (pair) {
      var idx = pair.indexOf("=");
      if (idx >= 0) {
        queryStringParameters[decodeURIComponent(pair.substring(0, idx))] =
          decodeURIComponent(pair.substring(idx + 1));
      }
    });
  }

  // __VU and __ITER are only defined inside VU iteration context, not in
  // setup()/teardown(). Use safe fallbacks so invoke() works from all phases.
  const vu = typeof __VU !== 'undefined' ? __VU : 0;
  const iter = typeof __ITER !== 'undefined' ? __ITER : 0;

  var event = {
    version: "2.0",
    routeKey: "$default",
    rawPath: rawPath,
    rawQueryString: rawQueryString,
    headers: headers || {},
    requestContext: {
      http: { method: method, path: rawPath },
      stage: "$default",
      requestId: "k6-" + vu + "-" + iter,
    },
    isBase64Encoded: false,
  };
  if (Object.keys(queryStringParameters).length > 0) {
    event.queryStringParameters = queryStringParameters;
  }

  var rieParams = Object.assign({}, params || {});
  rieParams.headers = Object.assign(
    { "Content-Type": "application/json" },
    rieParams.headers || {}
  );

  var res = http.post(
    BASE_URL + "/2015-03-31/functions/function/invocations",
    JSON.stringify(event),
    rieParams
  );

  // Extract the full Lambda response from the RIE invocation envelope.
  // The RIE POST response body is the Lambda / API Gateway response:
  //   { statusCode, headers: {...}, body: "...", isBase64Encoded, ... }
  // Checks (r.json(), r.headers, r.body) should see the Lambda's response,
  // not the RIE envelope. Host timing / transport data is preserved via
  // the prototype chain.
  var lambdaStatus = 0;
  var lambdaHeaders = {};
  var lambdaBody = "";
  try {
    var lambdaResponse = JSON.parse(res.body);
    lambdaStatus = lambdaResponse.statusCode || 0;
    var rawHeaders = lambdaResponse.headers || {};
    // Normalize: preserve original keys and add lowercase aliases so
    // checks like response.headers["cache-control"] work regardless of
    // the casing returned by the Lambda / API Gateway envelope.
    for (var h in rawHeaders) {
      if (Object.prototype.hasOwnProperty.call(rawHeaders, h)) {
        lambdaHeaders[h] = rawHeaders[h];
        var lower = h.toLowerCase();
        if (lower !== h) {
          lambdaHeaders[lower] = rawHeaders[h];
        }
      }
    }
    lambdaBody = lambdaResponse.body || "";
  } catch (e) {
    // keep defaults for invalid envelope
  }

  // Create a delegating wrapper: own properties project the Lambda response;
  // everything else (status, timings, url, request, etc.) falls through to
  // the host RIE response via Object.create() prototype chain.
  res = Object.assign(Object.create(res), {
    lambdaStatus: lambdaStatus,
    headers: lambdaHeaders,
    body: lambdaBody,
    json: function () {
      return JSON.parse(lambdaBody);
    },
  });
  return res;
}

// Test configuration
var hasOverrides = DURATION || VUS;

export const options = Object.assign(
  {
    thresholds: {
      http_req_duration: ["p(95)<500"], // 95% under 500ms
      http_req_failed: ["rate<0.1"], // Less than 10% errors
      cold_starts: ["rate<0.05"], // Less than 5% cold starts during steady state
      errors: ["rate<0.05"], // Less than 5% application errors
    },

    // Enhanced summary configuration for comprehensive reporting
    summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)", "count"],
    summaryTimeUnit: "ms",
  },
  hasOverrides
    ? {
        duration: DURATION || "30s",
        vus: VUS || 1,
      }
    : {
        stages: [
          // Warm-up phase - gentle ramp to establish baseline
          { duration: "30s", target: 5 }, // Warm up the Lambda

          // Load testing phases
          { duration: "1m", target: 20 }, // Normal load
          { duration: "2m", target: 50 }, // High load
          { duration: "1m", target: 100 }, // Stress test - trigger memory pressure
          { duration: "30s", target: 200 }, // Spike test - force cold starts

          // Cool down
          { duration: "30s", target: 0 },
        ],
      }
);

// Test data pools - realistic package names and scenarios
const testScenarios = {
  nugetPackages: ["Newtonsoft.Json", "Microsoft.Extensions.Http", "AutoMapper", "FluentValidation", "LocalStack.Client", "Serilog", "Polly", "MediatR", "EntityFramework", "NUnit"],

  githubPackages: [
    { org: "localstack-dotnet", package: "localstack.client" },
    { org: "localstack-dotnet", package: "localstack.client.extensions" },
    { org: "localstack-dotnet", package: "LocalStack.Aspire.Hosting" },
  ],

  testResults: [
    { platform: "linux", owner: "localstack-dotnet", repo: "localstack.client", branch: "main" },
    { platform: "windows", owner: "microsoft", repo: "vscode", branch: "main" },
    { platform: "linux", owner: "facebook", repo: "react", branch: "main" },
    { platform: "linux", owner: "dotnet", repo: "aspnetcore", branch: "release/8.0" },
    { platform: "windows", owner: "AutoMapper", repo: "AutoMapper", branch: "master" },
  ],
};

// Utility functions
function randomChoice(array) {
  return array[Math.floor(Math.random() * array.length)];
}

function detectColdStart(response) {
  // Look for cold start indicators in response time and headers
  const duration = response.timings.duration;
  const isColdStart = duration > 300 || (response.headers["x-amz-trace-id"] && duration > 100);
  coldStartRate.add(isColdStart ? 1 : 0);
  return isColdStart;
}

function detectMemoryPressure(response) {
  // Look for signs of memory pressure (slower responses, errors)
  const duration = response.timings.duration;
  if (duration > 1000 || (response.lambdaStatus || response.status) >= 500) {
    memoryPressureCounter.add(1);
    return true;
  }
  return false;
}

function checkCacheHeaders(response) {
  const etag = response.headers["etag"];
  const cacheControl = response.headers["cache-control"];
  const isFromCache = !!(etag && cacheControl);
  cacheHitRate.add(isFromCache ? 1 : 0);
  return isFromCache;
}

// Test scenarios
let requestCounter = 0;
let lastReportTime = 0;
const REPORT_INTERVAL = 30000; // Report every 30 seconds

function reportProgress() {
  const now = Date.now();
  if (now - lastReportTime >= REPORT_INTERVAL) {
    console.log(`📊 Progress: ${requestCounter} requests completed | VUs: ${__VU} | Time: ${Math.floor(__ITER * 2)}s`);
    lastReportTime = now;
  }
}

export default function () {
  requestCounter++;
  reportProgress();

  // testNugetPackageBadges();

  const scenario = Math.random();

  if (scenario < 0.4) {
    // 40% - NuGet package badges (most common)
    testNugetPackageBadges();
  } else if (scenario < 0.7) {
    // 30% - GitHub package badges
    testGithubPackageBadges();
  } else if (scenario < 0.85) {
    // 15% - Test result badges
    testResultBadges();
  } else if (scenario < 0.95) {
    // 10% - Health checks and redirects
    testHealthAndMisc();
  } else {
    // 5% - Edge cases and stress patterns
    testEdgeCases();
  }

  // Small random sleep to simulate real user behavior
  sleep(Math.random() * 2);
}

function testNugetPackageBadges() {
  group("NuGet Package Badges", () => {
    const packageName = randomChoice(testScenarios.nugetPackages);
    const path = `/badges/packages/nuget/${packageName}`;

    const response = invoke("GET", path, {
      Accept: "application/json",
      "User-Agent": "k6-perf-test/1.0",
    }, { tags: { scenario: "nuget_badge", package: packageName } });

    // Performance analysis
    const isColdStart = detectColdStart(response);
    const hasMemoryPressure = detectMemoryPressure(response);
    const isCached = checkCacheHeaders(response);

    // Validation checks
    check(response, {
      "status is 200": (r) => (r.lambdaStatus || r.status) === 200,
      "response time < 500ms": (r) => r.timings.duration < 500,
      "has badge data": (r) => r.json() && r.json().schemaVersion,
      "has cache headers": (r) => r.headers["cache-control"] !== undefined,
      "not a cold start": (r) => !isColdStart || Math.random() < 0.1, // Allow some cold starts
    });

    // Live reporting for slow responses
    if (response.timings.duration > 200) {
      console.log(`⚠️  Slow NuGet response: ${Math.round(response.timings.duration)}ms for ${packageName}`);
    }

    responseTimeP95.add(response.timings.duration);
    errorRate.add((response.lambdaStatus || response.status) >= 400 ? 1 : 0);
  });
}

function testGithubPackageBadges() {
  group("GitHub Package Badges", () => {
    const pkg = randomChoice(testScenarios.githubPackages);
    const path = `/badges/packages/github/${pkg.org}/${pkg.package}?prerelease=true`;

    const response = invoke("GET", path, {
      Accept: "application/json",
      "User-Agent": "k6-perf-test/1.0",
    }, { tags: { scenario: "github_badge", org: pkg.org, package: pkg.package } });

    detectColdStart(response);
    detectMemoryPressure(response);
    checkCacheHeaders(response);

    check(response, {
      "status is 200": (r) => (r.lambdaStatus || r.status) === 200,
      "response time < 1000ms": (r) => r.timings.duration < 1000, // GitHub API might be slower
      "has badge data": (r) => r.json() && r.json().schemaVersion,
    });

    // Live reporting for GitHub API issues
    if (response.timings.duration > 500) {
      console.log(`🐙 Slow GitHub response: ${Math.round(response.timings.duration)}ms for ${pkg.org}/${pkg.package}`);
    }

    responseTimeP95.add(response.timings.duration);
    errorRate.add((response.lambdaStatus || response.status) >= 400 ? 1 : 0);
  });
}

function testResultBadges() {
  group("Test Result Badges", () => {
    const test = randomChoice(testScenarios.testResults);
    const path = `/badges/tests/${test.platform}/${test.owner}/${test.repo}/${encodeURIComponent(test.branch)}`;

    const response = invoke("GET", path, null, {
      tags: { scenario: "test_badge", platform: test.platform },
    });

    detectColdStart(response);
    detectMemoryPressure(response);

    check(response, {
      "status is 200 or 404": (r) => (r.lambdaStatus || r.status) === 200 || (r.lambdaStatus || r.status) === 404, // 404 expected for non-existent test results
      "response time < 1000ms": (r) => r.timings.duration < 1000,
    });

    responseTimeP95.add(response.timings.duration);
    errorRate.add((response.lambdaStatus || response.status) >= 500 ? 1 : 0); // Only 5xx are real errors for this endpoint
  });
}

function testHealthAndMisc() {
  group("Health and Miscellaneous", () => {
    // Health check
    const healthResponse = invoke("GET", "/health", null, {
      tags: { scenario: "health_check" },
    });

    check(healthResponse, {
      "health check is 200": (r) => (r.lambdaStatus || r.status) === 200,
      "health check is fast": (r) => r.timings.duration < 100,
    });

    // Test a redirect endpoint
    if (Math.random() < 0.5) {
      const test = randomChoice(testScenarios.testResults);
      const redirectPath = `/redirect/test-results/${test.platform}/${test.owner}/${test.repo}/${encodeURIComponent(test.branch)}`;

      const redirectResponse = invoke("GET", redirectPath, null, {
        redirects: 0, // Don't follow redirects
        tags: { scenario: "redirect_test" },
      });

      check(redirectResponse, {
        "redirect status is 3xx": (r) => (r.lambdaStatus || r.status) >= 300 && (r.lambdaStatus || r.status) < 400,
      });
    }
  });
}

function testEdgeCases() {
  group("Edge Cases and Stress Patterns", () => {
    const edgeCase = Math.random();

    if (edgeCase < 0.3) {
      // URL-encoded package names
      const packageName = "Microsoft%2EExtensions%2EHttp";
      const response = invoke("GET", `/badges/packages/nuget/${packageName}`, null, {
        tags: { scenario: "edge_case", type: "url_encoded" },
      });

      check(response, {
        "handles URL encoding": (r) => (r.lambdaStatus || r.status) === 200,
      });
    } else if (edgeCase < 0.6) {
      // Rapid successive requests to same endpoint (cache testing)
      const packageName = randomChoice(testScenarios.nugetPackages);
      const path = `/badges/packages/nuget/${packageName}`;

      for (let i = 0; i < 3; i++) {
        const response = invoke("GET", path,
          i > 0 ? { "If-None-Match": "test-etag" } : null,
          { tags: { scenario: "edge_case", type: "cache_burst" } }
        );

        if (i === 0) {
          check(response, {
            "first request succeeds": (r) => (r.lambdaStatus || r.status) === 200,
          });
        }
      }
    } else {
      // Invalid routes (should be handled gracefully)
      const response = invoke("GET", "/badges/invalid/route/structure", null, {
        tags: { scenario: "edge_case", type: "invalid_route" },
      });

      check(response, {
        "invalid route returns 404": (r) => (r.lambdaStatus || r.status) === 404,
        "error response is fast": (r) => r.timings.duration < 200,
      });
    }
  });
}

// Handle setup and teardown
export function setup() {
  console.log("🚀 Starting BadgeSmith Lambda Performance Test");
  console.log(`📍 Target: ${BASE_URL}`);
  console.log("📊 Monitor AWS Lambda metrics in CloudWatch during this test:");
  console.log("   - Duration, Memory Usage, Cold Starts");
  console.log("   - Concurrent Executions, Throttles, Errors");
  console.log("   - Custom metrics from your application logs");

  // Warm up the Lambda
  const warmupResponse = invoke("GET", "/health");
  console.log(`🔥 Warmup response time: ${warmupResponse.timings.duration}ms`);

  return { startTime: new Date() };
}

export function teardown(data) {
  if (!data || !data.startTime) {
    console.log("✅ Performance test completed");
    console.log("📋 Check CloudWatch for metrics analysis");
    return;
  }

  try {
    const duration = Math.round((new Date() - new Date(data.startTime)) / 1000);
    console.log(`✅ Performance test completed in ${duration} seconds`);
  } catch (e) {
    console.log("✅ Performance test completed");
  }

  console.log("📋 Check CloudWatch for:");
  console.log("   - Peak memory usage patterns");
  console.log("   - Cold start frequency during load spikes");
  console.log("   - Error rates and timeout patterns");
  console.log("   - Cost implications of concurrent execution scaling");
}

// Custom summary handler for enhanced reporting
export function handleSummary(data) {
  // k6's default console summary (always show this)
  console.log("\n" + "=".repeat(80));
  console.log("🎯 BADGESMITH LAMBDA PERFORMANCE SUMMARY");
  console.log("=".repeat(80));

  // Let k6 handle the default summary display
  const defaultSummary = textSummary(data, { indent: "  ", enableColors: true });
  console.log(defaultSummary);

  // Add custom insights specific to Lambda performance
  console.log("\n🔍 Lambda-Specific Insights:");

  const metrics = data.metrics;

  // Cold start analysis
  if (metrics.cold_starts) {
    const coldStartRate = (metrics.cold_starts.values.rate * 100).toFixed(2);
    console.log(`   🧊 Cold Start Rate: ${coldStartRate}% (Target: <5%)`);

    if (coldStartRate > 5) {
      console.log("      ⚠️  Consider provisioned concurrency for critical workloads");
    } else {
      console.log("      ✅ Cold start rate within acceptable limits");
    }
  }

  // Memory pressure indicators
  if (metrics.memory_pressure_responses) {
    const memoryPressure = metrics.memory_pressure_responses.values.count;
    console.log(`   💾 Memory Pressure Events: ${memoryPressure}`);

    if (memoryPressure > 0) {
      console.log("      💡 Consider increasing Lambda memory allocation");
    }
  }

  // Cache effectiveness
  if (metrics.cache_hits) {
    const cacheHitRate = (metrics.cache_hits.values.rate * 100).toFixed(2);
    console.log(`   📦 Cache Hit Rate: ${cacheHitRate}%`);
  }

  // Performance recommendations
  console.log("\n💡 Recommendations:");

  if (metrics.http_req_duration) {
    const p95 = metrics.http_req_duration.values["p(95)"];
    if (p95 < 100) {
      console.log("   ✅ Excellent response times - Lambda is well optimized!");
    } else if (p95 > 500) {
      console.log("   ⚠️  High P95 latency - review memory allocation and cold starts");
    } else {
      console.log("   ✅ Good response times within acceptable range");
    }
  }

  if (metrics.http_req_failed) {
    const errorRate = (metrics.http_req_failed.values.rate * 100).toFixed(2);
    if (errorRate === "0.00") {
      console.log("   ✅ Zero error rate - excellent reliability!");
    } else if (errorRate > 5) {
      console.log("   ⚠️  High error rate - check Lambda logs and error handling");
    }
  }

  console.log("📊 Use this summary as your primary performance report!");
  console.log("   k6 provides comprehensive metrics out of the box");
  console.log("=".repeat(80));

  // Return the summary object for file exports (if any are configured)
  return {
    stdout: "", // We already handled console output above
    // Add any file exports here if needed via CLI --out flags
  };
}
