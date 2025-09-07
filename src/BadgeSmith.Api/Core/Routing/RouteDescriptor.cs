using BadgeSmith.Api.Core.Routing.Contracts;

namespace BadgeSmith.Api.Core.Routing;

internal record RouteDescriptor(string Name, string Method, Func<IRouteHandler> HandlerResolver, IRoutePattern Pattern);
