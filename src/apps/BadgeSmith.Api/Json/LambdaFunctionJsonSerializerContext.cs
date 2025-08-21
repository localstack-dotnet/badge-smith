using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Domain.Models;

namespace BadgeSmith.Api.Json;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ErrorDetail))]
[JsonSerializable(typeof(HealthCheckResponse))]
[JsonSerializable(typeof(ShieldsBadgeResponse))]
internal partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;
