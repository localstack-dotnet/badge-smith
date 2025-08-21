using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace BadgeSmith.Api.Json;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
internal partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext;
