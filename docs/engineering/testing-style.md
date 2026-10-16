# Testing Style

This document owns test authorship. Hand-written test code also follows
[coding style](coding-style.md); [quality gates](quality-gates.md) owns execution and evidence.
Exact project categories, contract topology, and harness commands live in the
[API test guide](../../tests/BadgeSmith.Api.Tests/README.md) and
[CDK test guide](../../tests/BadgeSmith.CDK.Tests/README.md).

## Layout And Names

- A test project should mirror the production area it verifies so source and test paths remain
  predictable.
- Test names use `Subject_Should_Expected_Behavior_When_Condition`. Keep code identifiers intact,
  underscore other words, put `Should` immediately after the subject, and end conditions with
  `When...`.
- Split classes and folders when real cohesion or navigation pressure appears, not in anticipation
  of hypothetical growth.

## Contracts And Seams

Test through public or internal contracts. The urge to test a private method is extraction
feedback, not a reason to use reflection. Interfaces need a real substitution seam, as defined by
[composition boundaries](coding-style.md#composition-boundaries).

The two similar provider implementations remain guarded by one shared scenario matrix run against
both; the [provider duplication threshold](coding-style.md#duplication-thresholds) still applies.
Internal test and Moq visibility is recorded in
[test triage](local-development.md#test-triage); consult the project file before moving internals.

## Allocation Contracts

Follow the existing [two-tier allocation discipline](coding-style.md#allocation-discipline): warmed
Release-mode synchronous facts gate named measured baselines, while asynchronous HTTP paths use
BenchmarkDotNet memory evidence rather than per-thread allocation gates. Raising a recorded ceiling
is a reviewed performance-contract change with a reason, not a test edit.

## Framework And Approval

Tests use xUnit v3 + Moq on VSTest, not TUnit or Microsoft.Testing.Platform. The
[root approval gate](../../AGENTS.md#approval-gate) governs the behavior under change: removing or
weakening coverage always requires approval, as do skipping, deleting, or substantially rewriting
tests to change verified behavior. Diagnose failures before proposing production changes; use
[local triage](local-development.md#test-triage) for the known environment and internals boundaries.
