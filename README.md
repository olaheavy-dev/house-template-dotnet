# house-template-dotnet

The .NET layer of my Copier project templates: an ASP.NET Core modular monolith under `backend/` for a project generated from [house-template-base](https://github.com/olaheavy-dev/house-template-base), with Postgres, Keycloak, OpenAPI for the Next.js layer, architecture tests and a signed container image.

Built through reviewed pull requests; the design is in the House template plan (".NET layers").

## Use it

Apply it after house-template-base (0.5.0 or later, with `dotnet` in its `stacks`), in the same project:

```
copier copy --trust -a .copier-answers.dotnet.yml gh:olaheavy-dev/house-template-dotnet .
make install && make check
```

It asks one question, the API's display name (default: the project name plus "API").

| Path | What it is |
|---|---|
| `global.json` | The .NET SDK (10.0.403, the October 2026 security release) and Microsoft.Testing.Platform as the test runner |
| `backend/Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.editorconfig` | The strict build: every analyzer rule as an error (the few exceptions say why), central package management with transitive pinning, committed lock files restored in locked mode, NuGet audit of every dependency, nuget.org only |
| `backend/src/Common/` | The shared kernel: ErrorOr for expected failures, Immediate.Handlers (source-generated handlers, no MediatR) with logging and FluentValidation pipeline steps, and RFC 9457 problem responses with a stable `code` |
| `backend/src/Modules/Platform/` | The example module, in the six projects every module has (Domain, Application, Infrastructure, Presentation, IntegrationEvents, PublicApi), with `GET /v1/platform/ping` |
| `backend/src/Api/Api` | The host: modules register themselves; Carter maps their endpoints |
| `backend/tests/` | NUnit unit tests, Reqnroll acceptance tests (feature files driving the API in memory) and integration tests |
| `mk/dotnet.mk` | `install-dotnet`, `check-dotnet`, `check-fast-dotnet`, `build-backend`, `test-backend`, `dev-backend` and `lock-backend`, joined to the base targets |
| `.claude/rules/dotnet.md`, `docs/references/dotnet.md` | Rules for coding agents in `backend/`, and the official docs for each pinned version (base's `.mcp.json` adds the Microsoft Learn MCP server) |

Project and namespace names are the same in every generated app (`Common.Domain`, `Modules.Platform.Application`), so the template's C# is plain code: it builds and tests in place, and Dependabot and CodeQL work on it directly.

Still to come (see the plan): architecture tests, OpenAPI, health checks, Keycloak, hardening, OpenTelemetry, EF Core and the outbox, the image and Compose.

## Develop the template

`uv run pytest` renders the layer and checks it. CI also builds and tests `template/backend` in place, and generates a real project from house-template-base plus this layer and runs its `make check`. After changing `template/backend/Directory.Packages.props`, run `dotnet restore template/backend/Backend.slnx --force-evaluate` and commit the lock files.

`main` is protected by [`.github/rulesets/main.json`](.github/rulesets/main.json): squash merges through pull requests, with the tests, workflow lint (actionlint and zizmor), the PR title check and CodeQL required, and CodeQL and zizmor findings blocking a merge. A test checks that the required checks and the jobs the workflows run on pull requests are the same set.

## Releases

release-please turns Conventional Commit titles into a release PR and, on merge, a `vX.Y.Z` tag. Projects update from those tags.
