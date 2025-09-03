using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;
using BadgeSmith.Api.Domain.Services.GitHub;
using BadgeSmith.Api.Domain.Services.Nuget;

namespace BadgeSmith.Api.Json;

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
internal partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;
