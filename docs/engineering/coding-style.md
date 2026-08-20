# Coding Style For Hand-Written C#

This document governs decomposition and composition choices that `.editorconfig`, analyzers, and
formatters cannot decide. Mechanical formatting and diagnostic severity remain owned by
`.editorconfig`, `Directory.Build.props`, and `BannedSymbols.txt`.

## Private Methods And Extraction

Private methods are for local mechanics and narrative flow. Code that carries behavior, policy, an
algorithm, or a concept that deserves a name belongs in a named collaborator.

Private methods are appropriate for:

- Narrative helpers that keep the entry operation readable.
- Local invariants or state handling that no other type needs.
- Small technical details with no independent domain meaning.

Extraction pressure exists when:

1. Private methods call one another in chains, hiding a workflow in one class.
2. A private method contains a business rule, policy, or branching algorithm.
3. A method takes five or more parameters; the parameter cluster may be a type.
4. A type mutates three or more fields through the workflow, creating temporal coupling.
5. A private method feels important enough to unit-test independently.
6. Generic names such as `Process`, `Handle`, `Do`, `Execute`, or `Run` hide the actual concept.

Name an extraction after what it is: `*Policy` or `*Validator` for rules, `*Classifier`,
`*Calculator`, or `*Normalizer` for algorithms, and `*Builder` or `*Composer` for assembly. A class
is a unit of meaning with a testable contract, not a namespace for unrelated static steps.

## Simplicity, Calibrated

Avoid both failure modes:

- **Private-method soup:** one large class with a private call graph, tuple state, concrete mutable
  collections, and behavior testable only through an end-to-end entry point.
- **Enterprise cosplay:** interfaces, factories, and layers around one caller and one behavior with
  no substitution or independent concept.

Extraction needs a concrete signal from the list above. An interface needs a real seam or a
substitution that production or tests actually perform.

## Composition Boundaries

BadgeSmith has three deliberate composition styles:

- `src/BadgeSmith.Api` is a Native AOT Lambda. `ApplicationRegistry` owns concrete implementations,
  often through interface-typed `Lazy<T>` fields; it reads required environment variables directly
  and does not use a DI or configuration framework.
- `tools/` is a file-based .NET application. It intentionally uses `HostApplicationBuilder`,
  `IServiceCollection`, `IConfiguration`, and constructor injection.
- `src/BadgeSmith.Host` is an Aspire AppHost and uses Aspire resource composition.

Do not migrate one project onto another project's composition model for aesthetic consistency.
Interfaces belong at real network, AWS, process, console, clock, configuration, or test-substitution
seams. New pure logic with one implementation defaults to a sealed concrete type; existing unsealed
types are not silently changed without reviewing their construction and extension contracts.

## Signature Hygiene

- Do not return tuples across type boundaries. A result worth exposing is worth a named record or
  result type. Tuples remain acceptable as local method mechanics.
- Avoid concrete mutable collection parameters on non-private members. Accept the weakest useful
  read-only abstraction or a domain type, while making ownership and allocation costs explicit.
- A parameter cluster repeated across signatures is a domain concept asking for a name.
- Guard invalid public inputs with BCL throw helpers. Use OneOf results for expected domain failures
  and exceptions for violated contracts or unexpected failures.
- Prefer immutable records and init-only state where that matches ownership; do not mistake
  `IReadOnly*` for deep immutability.

## Layout And Tests

- Keep feature behavior together in vertical slices; do not create repository-wide horizontal
  buckets by type kind.
- A test project should mirror the production area it verifies so source and test paths remain
  predictable.
- Split classes and folders when real cohesion or navigation pressure appears, not in anticipation
  of hypothetical growth.
- Test through public or internal contracts. The urge to test a private method is extraction
  feedback, not a reason to use reflection.

## Enforcement

The analyzer wall already covers method length, static opportunities, namespace placement, unused
code, async correctness, and AOT-safe regex. This document covers meaning and boundaries. A valid
analyzer exception is narrow, documented beside its file-scoped arbitration, and names the contract
that wins; do not weaken a rule globally to avoid a local design decision.
