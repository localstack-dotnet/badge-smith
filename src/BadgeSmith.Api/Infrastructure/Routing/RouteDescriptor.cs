using BadgeSmith.Api.Infrastructure.Handlers.Contracts;
using BadgeSmith.Api.Infrastructure.Routing.Contracts;

namespace BadgeSmith.Api.Infrastructure.Routing;

internal record RouteDescriptor(string Name, string Method, Func<IHandlerFactory, IRouteHandler> HandlerFactory, IRoutePattern Pattern);
