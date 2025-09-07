using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Features;
using BadgeSmith.Api.Features.GitHub;
using BadgeSmith.Api.Features.HealthCheck;
using BadgeSmith.Api.Features.NuGet;
using BadgeSmith.Api.Features.TestResults;
using BadgeSmith.Api.Features.TestResults.Models;

namespace BadgeSmith.Api.Core.Infrastructure;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.HttpDescription))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ErrorDetail))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
[JsonSerializable(typeof(NuGetIndexResponse))]
[JsonSerializable(typeof(GithubPackageVersion))]
[JsonSerializable(typeof(IReadOnlyList<GithubPackageVersion>))]
[JsonSerializable(typeof(PackageMetadata))]
[JsonSerializable(typeof(TestResultIngestionResponse))]
[JsonSerializable(typeof(TestResultPayload))]
internal partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;
