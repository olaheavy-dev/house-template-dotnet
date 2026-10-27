---
paths:
  - "backend/**"
  - "global.json"
---

# Backend (.NET)

Owned by house-template-dotnet. Before using an ASP.NET Core, EF Core or .NET API, check the official docs for the pinned version through the Microsoft Learn MCP server (`.mcp.json`) or `docs/references/dotnet.md`; don't rely on memory.

- **Layout:** a modular monolith in `backend/` (`Backend.slnx`). `src/Common/` is the shared kernel (`Common.Domain`, `Common.Application`, `Common.Infrastructure`, `Common.Presentation`); each module under `src/Modules/<Name>/` has six projects: `Domain`, `Application`, `Infrastructure`, `Presentation`, `IntegrationEvents` and `PublicApi`. `src/Api/Api` is the host. Project and namespace names are the same in every app (`Common.Domain`, `Modules.Platform.Application`).
- **Boundaries:** a module never references another module's projects except its `IntegrationEvents` and `PublicApi`. Domain depends on nothing but `Common.Domain`; Application on its Domain and `Common.Application`; Presentation on its Application and `Common.Presentation`; Infrastructure wires the module up (`Add<Name>Module`).
- **Requests:** every endpoint sends a command or query through `IDispatcher`; handlers implement `ICommandHandler`/`IQueryHandler`, are `internal sealed`, and are registered with `AddHandlersFrom(AssemblyReference.Assembly)`. No MediatR.
- **Results:** expected failures are values, never exceptions: handlers return `ErrorOr<T>` (`ErrorOr<Success>` for commands without a value) with `Error.NotFound("area.reason", "…")` and friends, the code stable and machine-readable. Endpoints answer with `ApiResults.Ok(result)`, `ApiResults.NoContent(result)` or `ApiResults.Problem(errors)` (RFC 9457: the first error sets the status and its code is the `code` extension; validation errors become one problem listing every field). Exceptions are for bugs and broken infrastructure.
- **Endpoints:** Carter, one `ICarterModule` per feature in the module's Presentation project, routes under `v1/`. A new module adds its Presentation assembly to the catalog in `Program.cs`.
- **Build:** every analyzer warning is an error (`backend/Directory.Build.props`, `AnalysisMode=All`); the exceptions in `backend/.editorconfig` each say why. Fix findings rather than suppressing them; a new suppression needs a reason beside it.
- **Packages:** versions only in `backend/Directory.Packages.props` (central package management); after a change run `make lock-backend` and commit the lock files. Never `dotnet add package --source` an unvetted feed (`backend/nuget.config` maps every package to nuget.org). No MediatR, FluentAssertions or SonarAnalyzer: their licences changed.
- **Tests:** NUnit on Microsoft.Testing.Platform with NUnit's own assertions; names read `Method_condition_result`.
  - Unit tests per project (`tests/<Project>.UnitTests`): domain rules and handlers.
  - Acceptance tests (`tests/AcceptanceTests`): Reqnroll feature files in the product's words, one per feature, driving the API through `WebApplicationFactory<Program>`. A new endpoint comes with its scenarios.
  - Integration tests (`tests/IntegrationTests`): technical checks on the host (wire format, HTTP details).
  - A bug fix comes with a test that fails without it.
- `make check` is what CI runs: locked restore, `dotnet format` check, the Release build and the tests with coverage.
