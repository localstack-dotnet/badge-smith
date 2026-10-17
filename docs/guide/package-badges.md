# Package badges

BadgeSmith supplies Shields.io endpoint content for NuGet.org and GitHub Packages. Choose the
provider and package, optionally select a version range, then use that URL as the `url` query
value of `https://img.shields.io/endpoint`.

## Choose the package source

```text
GET /badges/packages/nuget/{package}[?version={range}&prerelease={bool}]
GET /badges/packages/github/{org}/{package}[?version={range}&prerelease={bool}]
```

For the public deployment:

```text
https://api.localstackfor.net/badges/packages/nuget/Newtonsoft.Json
https://api.localstackfor.net/badges/packages/github/localstack-dotnet/localstack.client?version=(1.0,2.0)
```

The NuGet route has no organization segment. The GitHub route requires one and uses the
deployment's organization-scoped package credential. Badge readers do not send that credential;
self-hosting operators configure it through the [secret mapping reference](../../tools/README.md#secret-mapping).
The package credential and the test-data submission secret have different roles.

## Select versions

The badge shows the highest matching package version, not the most recently uploaded package.

- Omit `version` to select without a range constraint, or supply a NuGet version range such as
  `[6.0,8.0)` or `(1.0,2.0)`.
- Omit `prerelease`, or use `prerelease=false`, for stable versions only.
- Use `prerelease=true` to include prereleases in the same maximum-version selection. It does
  not select a named prerelease channel or mean “most recent CI build.”

For example, the endpoint URL for versions from 6.0 inclusive to 8.0 exclusive, including
prereleases, is:

```text
https://api.localstackfor.net/badges/packages/nuget/Newtonsoft.Json?version=%5B6.0%2C8.0%29&prerelease=true
```

## Put it in a README

Encode the whole BadgeSmith endpoint URL when nesting it inside Shields.io's `url` parameter.
In particular, the endpoint's `?` and `&` must remain part of that nested URL.

```markdown
[![NuGet](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Fpackages%2Fnuget%2FNewtonsoft.Json%3Fversion%3D%255B6.0%252C8.0%2529%26prerelease%3Dtrue)](https://www.nuget.org/packages/Newtonsoft.Json/)
```

The image uses BadgeSmith's content; the outer Markdown link is your chosen package page.
Replace the deployment base URL for self-hosting. Existing [live examples](../../README.md)
show the LocalStack.NET version tracks. Public response caching and validators are described
in the [runtime contract](../architecture/runtime-and-routing.md#cache-strategy).

Next: [test badges and report links](test-results.md).
