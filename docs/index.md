# Documentation index

The documentation of the MediatR union POC, split by topic. Back to the [project README](../README.md).

The sections below cover the mechanics and vocabulary the pattern is built on, roughly in the order
they build on each other. Read what's relevant to what you're doing; the [Glossary](glossary.md#glossary) at
the end is reference material, not required front-to-back reading.

## The pattern

| Page | What it covers |
| --- | --- |
| [Scope of the pattern and the POC](scope.md) | What the union-as-response convention provides and what it is not, what the POC demonstrates and what it deliberately leaves out. |
| [The C# `union` type](union-type.md) | What a `union` declaration is, switch-and-unwrap, and the static abstract members that let generic code build one. |
| [Case types](case-types.md) | The shared case types, why they are meaning-free, and how a failed commit is classified per union. |
| [No exceptions for expected outcomes](no-exceptions.md) | Why expected outcomes are returned, not thrown, and what this repo does instead. |
| [Request lifecycle](request-lifecycle.md) | The MediatR pipeline in order, and what commits or rolls back for each message. |
| [Transactions and Unit of Work](transactions.md) | What rollback undoes and why, and the one-session unit of work. |
| [Vogen value objects](value-objects.md) | Avoiding primitive obsession with Vogen. |

## Guides

| Page | What it covers |
| --- | --- |
| [Adding a new command or query](adding-a-command.md) | Step by step: a new operation, its union, handler, validator and endpoint. |
| [Worked example: UpdateProductCommand](worked-example-update.md) | One command followed case by case through the pipeline and the controller. |
| [Extending the pattern: syncing a search index](extending-search-index.md) | A sketch of MediatR notifications and eventual consistency. |
| [Speculative shared case types](speculative-case-types.md) | Case types a larger API might add, and what each would mean. |
| [Testing](testing.md) | How the union mechanics, pipeline and API are tested, and the compile-time probes. |

## HTTP API

| Page | What it covers |
| --- | --- |
| [The HTTP contract and API versioning](http-contract.md) | Every endpoint and outcome, and the URL-segment versioning scheme. |
| [Optimistic concurrency](concurrency.md) | `ProductVersion`, the weak `ETag`, `If-Match` and duplicate product names. |
| [Partial updates: PATCH](patch.md) | JSON Merge Patch and the `Optional<T>` binding behind it. |
| [Listing products](listing.md) | Filtering, sorting and paging, with `X-Total-Count` and `Link` headers. |

## Security

| Page | What it covers |
| --- | --- |
| [Authorization](authorization.md) | Role-based and resource-based authorization, JWT identity and how to gate a new command. |
| [Impersonation](impersonation.md) | The controlled authentication bypass: tokens, configuration and its audit trail. |
| [Audit stream](audit.md) | The separate append-only record of security-relevant actions. |

## Operations

| Page | What it covers |
| --- | --- |
| [Trace id, unhandled exceptions and logging](logging-and-errors.md) | The trace id on every response, the global exception handler and Serilog. |
| [Health checks, CORS, rate limiting, timeouts and the OpenAPI check](operations.md) | Health endpoints and the options convention, CORS, per-caller rate limits, request timeouts and the OpenAPI snapshot test. |

## Reference

| Page | What it covers |
| --- | --- |
| [Notes and gotchas](notes-and-gotchas.md) | Pitfalls and non-obvious decisions collected in one place. |
| [Glossary](glossary.md) | Architectural patterns, C# concepts, MediatR and Vogen vocabulary, and this repo's own types. |

## Other documents

| Document | What it covers |
| --- | --- |
| [Features](Features.md) | A one-page feature list: what each feature does and why it is worth having. |
| [Hardening plan](Hardening-Plan.md) | The plan for the security and operations hardening work. |
| [Documentation split plan](README-Split-Plan.md) | How the README was split into this documentation set. |
| [Research notes](research/) | Source-backed research on the union type, MediatR, FluentValidation, Vogen and a CI build hang. |

## Project documentation

| Project | Responsibility |
| --- | --- |
| [Domain](../src/MediatrUnionPoc.Domain/README.md) | Entities, value objects, repository and unit-of-work interfaces. |
| [Application](../src/MediatrUnionPoc.Application/README.md) | Commands, queries, handlers, unions, validators and pipeline behaviors. |
| [Infrastructure](../src/MediatrUnionPoc.Infrastructure/README.md) | EF Core over SQLite: the repository and unit of work. |
| [Api](../src/MediatrUnionPoc.Api/README.md) | Controllers, HTTP mapping, authentication, audit, versioning and hosting. |
| [Domain.Tests](../tests/MediatrUnionPoc.Domain.Tests/README.md) | Unit tests for the domain. |
| [Application.Tests](../tests/MediatrUnionPoc.Application.Tests/README.md) | Union mechanics, behaviors, handlers, validators, authorization. |
| [Infrastructure.IntegrationTests](../tests/MediatrUnionPoc.Infrastructure.IntegrationTests/README.md) | The real EF Core SQLite provider, end to end. |
| [Api.IntegrationTests](../tests/MediatrUnionPoc.Api.IntegrationTests/README.md) | The real HTTP host against SQLite. |
| [ArchitectureTests](../tests/MediatrUnionPoc.ArchitectureTests/README.md) | Layering rules and the documentation link check. |
| [CompileTimeChecks](../tests/CompileTimeChecks/README.md) | Probe projects proving non-exhaustive switches fail to compile. |
