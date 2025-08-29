using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Domain.Models;

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
internal partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;
