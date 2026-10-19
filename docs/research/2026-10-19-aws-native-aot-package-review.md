# AWS Packages And Native AOT: Evidence Review

Evidence date: **2026-10-19**. Scope: package/source facts and official Native AOT guidance,
not an approved upgrade, serializer replacement, or performance result. Current policy remains in
[platform and delivery](../architecture/platform-and-delivery.md#native-aot-and-composition).

## Package Currency

Public NuGet stable-version indexes checked at **2026-10-19T14:16:24.719Z**; each selected latest
version was also checked as listed in NuGet registration metadata. The
[version snapshot](baselines/2026-10-19-aws-package-versions.json) records exact pins, publication
dates, API URL templates and dependency minima. This is feed metadata, not a new restore graph or
an upgraded-binary verification. The [central props file](../../Directory.Packages.props) remains
the owner of actual configured versions.

| Package | Repository pin | Latest listed stable |
|---|---:|---:|
| Amazon.Lambda.Serialization.SystemTextJson | 2.4.5 | 3.0.1 |
| Amazon.Lambda.RuntimeSupport | 2.1.2 | 2.3.0 |
| Amazon.Lambda.Core | 3.1.1 | 3.4.0 |
| Amazon.Lambda.APIGatewayEvents | 3.0.0 | 3.0.1 |
| AWSSDK.Core | 4.0.100.6 | 4.0.102.11 |
| AWSSDK.DynamoDBv2 | 4.0.101.1 | 4.0.107.3 |
| AWSSDK.SecretsManager | 4.0.100.3 | 4.0.100.19 |
| AWSSDK.Extensions.NETCore.Setup | 4.0.100.5 | 4.0.102.4 |
| Amazon.Lambda.TestUtilities | 4.1.0 | 4.3.0 |
| Amazon.CDK.Lib | 2.263.0 | 2.273.0 |
| Aspire.Hosting.AWS | 13.3.1 | 13.7.2 |

The latest serializer requires **Lambda.Core >=3.3.0**; RuntimeSupport2.3.0 and TestUtilities4.3.0
require **Core >=3.4.0**. Latest DynamoDB/SecretsManager require **AWSSDK.Core >=4.0.102.11 and <5**.
These are coupled update groups, not independent pin edits. CDK, test utilities and local Aspire
freshness must not be confused with shipped Lambda startup. No package upgrade is performed here.

## BadgeSmith Snapshot

- [Central pins](../../Directory.Packages.props) include
  `Amazon.Lambda.Serialization.SystemTextJson` **2.4.5** and
  `Amazon.Lambda.RuntimeSupport` **2.1.2**. The serializer pin comment identifies missing
  trimmability metadata in the **3.0.0 net10.0 asset**, not a ban on all later releases.
- [Shared build settings](../../Directory.Build.props) select `net10.0`;
  [the API project](../../src/BadgeSmith.Api/BadgeSmith.Api.csproj) has `PublishAot=true`,
  `TrimMode=partial`, and `StripSymbols=true`. The production
  [Dockerfile](../../src/BadgeSmith.Api/Dockerfile) disables telemetry and LocalStack during publish
  and uses `public.ecr.aws/lambda/provided:al2023` for its Lambda image.
- [Program.cs](../../src/BadgeSmith.Api/Program.cs) passes
  `new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>()` to
  `LambdaBootstrapBuilder.Create`. The
  [generated-context declaration](../../src/BadgeSmith.Api/Core/Infrastructure/LambdaFunctionJsonSerializerContext.cs)
  registers HTTP API v2 request/response types and application payloads. This is source-generated
  JSON, not proof that every dependency or initialization operation is reflection-free.

## Reflection-Safe Is Not Reflection-Free

1. **Pinned-release source:** AWS's `release_2026-01-15` source declares serializer version
   **2.4.5**. Its generic context parameter has
   `DynamicallyAccessedMembers(PublicConstructors)`; construction calls
   `typeof(TSGContext).GetConstructor(new Type[] { typeof(JsonSerializerOptions) })` and then
   `constructor.Invoke(...)`. This is bounded, annotated reflection for context construction,
   not reflection-based discovery of payload properties. Serialization/deserialization obtain
   generated `JsonTypeInfo<T>` from the context. [S1], [S2]
2. **Pinned binary observation, separately supplied:** the coordinating review decompiled the
   cached 2.4.5 DLL and reported the same constructor lookup/invocation and annotation. This
   research did not repeat that binary inspection; the independently read release source above
   corroborates the mechanism, rather than establishing binary identity by itself. [S1], [S2]
3. **Current upstream source:** the same constructor mechanism is present in AWS `master` as
   observed at commit `21d6d91456d967994bcd7da4662427f1f3494ea3`. That source observation does
   **not** establish the contents of the latest published NuGet binary. [S3]
4. **Do not use the obsolete context-instance constructor as a shortcut.** AWS explicitly marks it
   obsolete because the supplied context may lack Lambda's required `JsonSerializerOptions`,
   leading to confusing `NullReferenceException`s. Its warning says the other constructors'
   annotated reflection calls are Native AOT compatible. [S1], [S3]
5. **The serializer contract is larger than two JSON calls.** AWS's base serializer supplies
   null omission, case-insensitive property matching, `AwsNamingPolicy`, DateTime/MemoryStream/
   ConstantClass/byte-array converters, writer options, `LAMBDA_NET_SERIALIZER_DEBUG`, exception
   wrapping, and stream handling. Any future equivalence evaluation must account for these;
   this review proposes no custom implementation. [S4]

**Answer:** source-generated payload metadata avoids reflection-based JSON property discovery.
The AWS wrapper itself still uses annotated reflection. “Native AOT compatible” and
“universally reflection-free” are different claims; the latter is false for this constructor. [S1], [S3]

## Explicit Upstream AOT Changes

- AWS's **2026-08-17 changelog** records serializer **3.0.1** fixing Native AOT IL3050/IL3053
  analysis warnings by making **all target frameworks** trimmable; previously only `net8.0` was
  marked and the `net10.0` assembly was not. The same release records the all-framework
  trimmability fix for `Amazon.Lambda.APIGatewayEvents` **3.0.1**. This directly relates to the
  repository's 3.0.0 pin rationale, but does not establish compatibility of an upgraded application.
  Current serializer project source also has unconditional `IsTrimmable=true`. [S5], [S6]
- The **2026-06-22 #3** entry describes RuntimeSupport **2.1.2** as a response-streaming
  type-load fix, not a removal-of-reflection or startup-speed release. No timing or size benefit
  for BadgeSmith follows from these release notes. [S5]

## AWS SDK v4: AOT Support Has Boundaries

- BadgeSmith already pins v4 Core, DynamoDB, and Secrets Manager packages in
  [central configuration](../../Directory.Packages.props). AWS's SDK repository says low-level
  service clients are generated from service models. That generation alone does not establish
  absence of reflection in every shared runtime, credential, extension, or high-level model path. [S7]
- The v4 migration guide documents a concrete AOT improvement in
  `AWSSDK.Extensions.NETCore.Setup`: on .NET 8+, C# static interface methods replace assembly
  type loading/string-derived service-client construction. This is a specific supported path,
  not a guarantee that all AWS packages are reflection-free. [S8]
- The same guide documents DynamoDB Document/Object Persistence Model nested-type trimming
  limitations and `DynamicDependency` preservation as a workaround. Therefore AOT-compatible
  service calls must not be generalized into unrestricted reflection-free object mapping. This
   [BadgeSmith's current storage source](../../src/BadgeSmith.Api/Core/ApplicationRegistry.cs)
   constructs the low-level `AmazonDynamoDBClient`; this does not certify unrelated high-level
   DynamoDB model paths. [S8]

## Official Best Practices And Their Limits

1. **Register the actual JSON types.** AWS requires source-generated serialization for Native AOT:
   include handler inputs, outputs, and custom types; use generated metadata for manual JSON calls
   too. Missing registrations can fail at runtime. BadgeSmith's existing context is the relevant
   registration point, but this review did not audit every serialization call. [S9], [S10]
2. **Use official templates as references, not blanket proof.** AWS's inspected .NET 10 Native AOT
   template uses `PublishAot`, partial trimming, a generated context, and `LambdaBootstrapBuilder`.
   Its defaults select managed `dotnet10`, whereas BadgeSmith uses `provided.al2023`; these are
   distinct deployment choices. The template still carries an older broad “SDK does not support
   trimming” comment, so use the newer, feature-specific v4 documentation for SDK boundaries.
   Template package pins are not a live package-currency inventory. [S8], [S11], [S12], [S13]
3. **Match build OS and architecture to deployment.** AWS's Native AOT guide requires an AL2023
   compilation environment for its AL2023 runtime and strongly recommends matching processor
   architecture. Its page is still framed around .NET 8; do not mislabel it as a .NET 10-specific
   deployment recipe. BadgeSmith's Dockerfile starts its builder from
   `mcr.microsoft.com/dotnet/sdk:10.0`; an AL2023 final image alone does not prove a matching build
   environment. The prior [official-image study](2026-10-19-native-first-request-attribution.md#4-official-al2023-target-layout-differs-default-only-execution-works)
   did execute its exact historical B ELF successfully in AL2023 x64. That is artifact-specific
   compatibility evidence, not a general ABI guarantee for future SDK-image builds or ARM64. [S9]
4. **Treat package changes as new native-verification inputs.** AWS warns that trimming can remove
   needed code and that successful compilation is insufficient; its template calls for additional
   deployed Native AOT testing. Inspect warnings and exercise the actual published artifact and
   relevant dependency paths after an approved change. Partial trimming or broad roots are not
   automatic correctness guarantees. No upgrade or warning suppression is authorized here. [S9], [S11]
5. **Reuse clients; defer only genuinely unused paths.** AWS recommends initializing SDK clients
   outside the handler for execution-environment reuse, minimizing dependencies, and lazily loading
   objects used only on particular paths. This is not a blanket recommendation to make every
   dependency lazy or to recreate clients per request. [S10], [S14]
6. **Keep INIT and first invocation separate.** AWS defines Init as runtime/static initialization
   before Invoke. Moving required work into first use can lower INIT without reducing a cold
   request's latency. BadgeSmith already requires both measurements in
   [its measurement policy](../architecture/platform-and-delivery.md#performance-goals).
   Warm pings or provisioned concurrency are not evidence that its on-demand cold-start goal was
   met; this review proposes neither workaround. [S14], [S15]
7. **Logging needs its own AOT-safe path.** AWS documents `ILambdaLogger`/stdout/stderr logging;
   source-generated request JSON does not certify object destructuring elsewhere. If Powertools
   logging is used, its official AOT guidance requires generated contexts/resolvers for custom
   types and recommends concrete registered types over anonymous objects. This is conditional
    guidance, not a recommendation to add another logging package. [S15], [S16]

## Consequence For The Startup Investigation

Do not treat annotated constructor reflection as a demonstrated defect or introduce a custom
serializer merely to eliminate it. First assess the supported AWS package path: upstream records
the precise trimmability fix behind the older pin. Any proposed upgrade is a separately approved
package change requiring dependency-aligned restore, contract tests, actual Native AOT warning
checks and artifact execution. Latest-package binary metadata and BadgeSmith compatibility remain
unverified; no startup, memory or package-size improvement follows from package freshness.

## Verification And Unknowns

Read the repository files linked above and fetched the cited official documentation, release source,
current source, changelog, and template files. Context7 was used for initial documentation discovery;
primary pages/source were checked directly. Source establishes mechanisms, not measured benefits.

Not established: latest-package binary identity, full dependency reflection inventory, whole-app AOT
safety, deployed artifact behavior, or improvements to INIT, first-request duration, memory, or ZIP
size. No package update/install/restore, binary download, build, test, native publish, benchmark,
Docker execution, deployment, AWS operation, or production-code change was performed by this research.

## Primary Sources

[S1]: https://github.com/aws/aws-lambda-dotnet/blob/b9901a9013cd6ed27986b2e912f32363e5fbf5cd/Libraries/src/Amazon.Lambda.Serialization.SystemTextJson/SourceGeneratorLambdaJsonSerializer.cs
[S2]: https://github.com/aws/aws-lambda-dotnet/blob/b9901a9013cd6ed27986b2e912f32363e5fbf5cd/Libraries/src/Amazon.Lambda.Serialization.SystemTextJson/Amazon.Lambda.Serialization.SystemTextJson.csproj
[S3]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/Libraries/src/Amazon.Lambda.Serialization.SystemTextJson/SourceGeneratorLambdaJsonSerializer.cs
[S4]: https://github.com/aws/aws-lambda-dotnet/blob/b9901a9013cd6ed27986b2e912f32363e5fbf5cd/Libraries/src/Amazon.Lambda.Serialization.SystemTextJson/AbstractLambdaJsonSerializer.cs
[S5]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/CHANGELOG.md
[S6]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/Libraries/src/Amazon.Lambda.Serialization.SystemTextJson/Amazon.Lambda.Serialization.SystemTextJson.csproj
[S7]: https://github.com/aws/aws-sdk-net/blob/main/README.md#code-generator
[S8]: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/net-dg-v4.html
[S9]: https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html
[S10]: https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html
[S11]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/Blueprints/BlueprintDefinitions/vs2026/NativeAOTFunction/template/src/BlueprintBaseName.1/Function.cs
[S12]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/Blueprints/BlueprintDefinitions/vs2026/NativeAOTFunction/template/src/BlueprintBaseName.1/BlueprintBaseName.1.csproj
[S13]: https://github.com/aws/aws-lambda-dotnet/blob/21d6d91456d967994bcd7da4662427f1f3494ea3/Blueprints/BlueprintDefinitions/vs2026/NativeAOTFunction/template/src/BlueprintBaseName.1/aws-lambda-tools-defaults.json
[S14]: https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtime-environment.html
[S15]: https://docs.aws.amazon.com/lambda/latest/dg/csharp-logging.html
[S16]: https://docs.aws.amazon.com/powertools/dotnet/core/logging/#aot-support
