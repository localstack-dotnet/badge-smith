using BadgeSmith.Api.Handlers.Contracts;
using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Routing;

internal record RouteDescriptor(string Name, string Method, bool RequiresAuth, Func<IHandlerFactory, IRouteHandler> HandlerFactory, IRoutePattern Pattern);
