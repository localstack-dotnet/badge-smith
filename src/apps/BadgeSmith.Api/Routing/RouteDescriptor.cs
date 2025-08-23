using BadgeSmith.Api.Routing.Contracts;

namespace BadgeSmith.Api.Routing;

internal record RouteDescriptor(string Name, string Method, bool RequiresAuth, Type HandlerType, IRoutePattern Pattern);
