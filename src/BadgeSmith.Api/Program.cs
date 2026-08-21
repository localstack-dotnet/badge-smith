#if !ENABLE_TELEMETRY
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using BadgeSmith.Api.Core;
using BadgeSmith.Api.Core.Infrastructure;
using LoggerFactory = BadgeSmith.Api.Core.Observability.LoggerFactory;

var apiRouter = ApplicationRegistry.ApiRouter;
var logger = LoggerFactory.CreateLogger<Program>();
var processor = new LambdaRequestProcessor(apiRouter, logger);

Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> handler = processor.HandleAsync;

var jsonSerializer = new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>();
var lambdaBootstrap = LambdaBootstrapBuilder.Create(handler, jsonSerializer).Build();

await lambdaBootstrap.RunAsync().ConfigureAwait(false);
return;
#endif
