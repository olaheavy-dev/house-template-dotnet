# .NET references

Owned by house-template-dotnet. The versions this backend pins, with the official docs for each. Check these (or the Microsoft Learn MCP server in `.mcp.json`) before using an API; update the table when Dependabot moves a version.

| What | Pinned | Docs |
|---|---|---|
| .NET SDK and runtime | SDK 10.0.403, runtime 10.0.14 (`global.json`) | https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview |
| ASP.NET Core minimal APIs | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis |
| Problem Details (RFC 9457) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api |
| Health checks | 10.0 | https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks |
| OpenAPI, generated at build time | Microsoft.AspNetCore.OpenApi and Microsoft.Extensions.ApiDescription.Server 10.0.14 | https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi |
| OpenTelemetry for .NET | 1.20.1 (Extensions.Hosting, OTLP exporter, ASP.NET Core, HTTP and runtime instrumentation) | https://opentelemetry.io/docs/languages/dotnet/ |
| Service defaults (the shape this project follows, without service discovery) | .NET Aspire | https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults |
| HTTP logging (the access log) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/http-logging/ |
| JSON console logs | 10.0 | https://learn.microsoft.com/dotnet/core/extensions/console-log-formatter |
| HTTP resilience (`AddStandardResilienceHandler`) | Microsoft.Extensions.Http.Resilience 10.11.0 | https://learn.microsoft.com/dotnet/core/resilience/http-resilience |
| Rate limiting | 10.0 | https://learn.microsoft.com/aspnet/core/performance/rate-limit |
| Request timeouts | 10.0 | https://learn.microsoft.com/aspnet/core/performance/timeouts |
| Kestrel limits (request body size) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/options |
| CORS | 10.0 | https://learn.microsoft.com/aspnet/core/security/cors |
| Host filtering (`AllowedHosts`) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/host-filtering |
| Server-Sent Events (`TypedResults.ServerSentEvents`) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses#server-sent-events-sse |
| Options validation | 10.0 | https://learn.microsoft.com/dotnet/core/extensions/options#options-validation |
| JWT bearer authentication | Microsoft.AspNetCore.Authentication.JwtBearer 10.0.14 | https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication |
| Minimal API authorization (`RequireAuthorization`) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security |
| Keycloak (local sign-in, realm import) | 26.8.1 | https://www.keycloak.org/server/importExport |
| Wolverine (transactional outbox, inbox, durable local queues; no broker) | WolverineFx, .EntityFrameworkCore, .Postgresql, .RuntimeCompilation 6.56.0 | https://wolverinefx.net/guide/durability/efcore/ |
| Wolverine code generation (`codegen write`, `TypeLoadMode.Static`) | 6.56.0 | https://wolverinefx.net/guide/codegen.html |
| Idempotency-Key header (IETF draft; own implementation, see the supply-chain table) | draft-ietf-httpapi-idempotency-key-header | https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/ |
| Testcontainers for .NET (Keycloak, PostgreSQL) | 4.16.0 | https://dotnet.testcontainers.org/modules/ |
| EF Core (Npgsql provider, migrations, `dotnet ef`) | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Microsoft.EntityFrameworkCore.Design and dotnet-ef 10.0.14 | https://learn.microsoft.com/ef/core/ |
| Npgsql concurrency tokens (`xmin`) | 10.0 | https://www.npgsql.org/efcore/modeling/concurrency.html |
| Health checks for EF Core | Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 10.0.14 | https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks#entity-framework-core-dbcontext-probe |
| Security headers | NetEscapades.AspNetCore.SecurityHeaders 1.3.1 | https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders |
| JetBrains.Annotations (`[UsedImplicitly]` for what a framework uses) | 2026.2.0 | https://www.jetbrains.com/help/resharper/Reference__Code_Annotation_Attributes.html |
| Scalar (API reference, development only) | Scalar.AspNetCore 2.18.3 | https://scalar.com/products/api-references/integrations/aspnetcore/integration |
| Minimal APIs (endpoints) | ASP.NET Core | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers |
| ErrorOr | 2.1.1 | https://github.com/amantinband/error-or |
| Immediate.Handlers | 4.2.0 | https://github.com/ImmediatePlatform/Immediate.Handlers |
| FluentValidation | 12.2.0 | https://docs.fluentvalidation.net |
| Central package management | NuGet | https://learn.microsoft.com/nuget/consume-packages/central-package-management |
| Lock files and locked restore | NuGet | https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies |
| Code analysis | .NET 10 analyzers, `AnalysisMode=All` | https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview |
| NUnit | 5.0.0 | https://docs.nunit.org |
| ArchUnitNET | 0.13.5 (TngTech.ArchUnitNET) | https://archunitnet.readthedocs.io |
| Mono.Cecil (IL reading in the architecture tests) | 0.11.6 | https://github.com/jbevain/cecil/wiki |
| `dotnet new` templates (`make module`) | .NET 10 SDK | https://learn.microsoft.com/dotnet/core/tools/custom-templates |
| Reqnroll | 3.3.4 (Reqnroll.NUnit) | https://docs.reqnroll.net |
| Microsoft.Testing.Platform | via NUnit3TestAdapter 6.3.1 | https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro |
| Integration tests (`WebApplicationFactory`) | Microsoft.AspNetCore.Mvc.Testing 10.0.14 | https://learn.microsoft.com/aspnet/core/test/integration-tests |
| Coverage | coverlet.MTP 10.1.0 | https://github.com/coverlet-coverage/coverlet |

## Dependency health

Checked 27 Oct 2026; review this table whenever Dependabot proposes a major upgrade. `make package-health` (and CI, when package versions change) fails for a package with no release in a year unless `backend/package-health.json` accepts it with a reason and an end date. `make check` fails on any licence outside `backend/licenses/allowed.json`.

| Package | Downloads | Maintainers | Latest release | Licence | Exit route |
|---|---|---|---|---|---|
| FluentValidation | 1.1 billion | 311 contributors | 12.2.0, Oct 2026 | Apache-2.0 | The de facto standard; validators are plain classes |
| Microsoft.AspNetCore.* / Extensions.* | — | Microsoft | 10.0.14, Oct 2026 | MIT | Part of .NET |
| NUnit, NUnit3TestAdapter, NUnit.Analyzers | 100M+ | NUnit team | 5.0.0, Sep 2026 | MIT | Tests only |
| Reqnroll | 31 million | 271 contributors | 3.3.4, Mar 2026 | BSD-3-Clause | Feature files are plain Gherkin |
| ErrorOr | 12.9 million | one main author | 2.1.1, May 2026 | MIT (no licence metadata in the package; see `backend/licenses/overrides.json`) | Copy its MIT source into Common.Domain with the same API |
| Npgsql.EntityFrameworkCore.PostgreSQL | 538 million | Shay Rojansky and the Npgsql team | 10.0.3, Jul 2026 (repository active, Oct 2026) | PostgreSQL licence (permissive, OSI-approved; added to `backend/licenses/allowed.json`) | Another EF Core provider, or Dapper over Npgsql; modules touch EF only in Infrastructure |
| Testcontainers.PostgreSql | 54 million | Testcontainers (Docker) | 4.16.0, Oct 2026 | MIT | Tests only; `docker run` in a fixture |
| Microsoft.EntityFrameworkCore.Design, dotnet-ef, Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 1.1 billion, 135 million, 158 million | Microsoft | 10.0.14, Oct 2026 | MIT | Part of .NET; Design and dotnet-ef are build-time only |
| WolverineFx, WolverineFx.EntityFrameworkCore, WolverineFx.Postgresql, WolverineFx.RuntimeCompilation | 10.9 million (WolverineFx) | Jeremy D. Miller and contributors | 6.56.0, Oct 2026 (repository active, Oct 2026) | MIT (re-check the licence on every upgrade: record "MIT at 6.56.0") | Modules touch it only through `IUnitOfWork` and by convention-named subscribers (an architecture test enforces it), so it is replaceable by another outbox; a hand-written outbox is the fallback. RuntimeCompilation is Debug only |
| IdempotentAPI (considered, not adopted) | 0.62 million (IdempotentAPI.MinimalAPI: 0.07 million) | Ioannis Kyriakidis | 2.6.0 / 3.2.0; repository last pushed Nov 2024 | MIT | The filter is about 150 lines over one table, written in-house because no maintained package fits: the dedicated alternatives found (Idempotency.AspNetCore, IdempotencyGuard, IdempotencyShield, Delobytes.AspNetCore.Idempotency, DKNet.AspCore.Idempotency, Elarion.Idempotency.EntityFrameworkCore) each have under 11 thousand downloads, and CoreEx and Mvp24Hours are wider frameworks. Replace it with IdempotentAPI or any package that gets a release again |
| Mono.Cecil | 116 million | jbevain | 0.11.6, **Oct 2024** (accepted until Apr 2027) | MIT | Goes with ArchUnitNET |
| TngTech.ArchUnitNET | 10.6 million | TNG | 0.13.5, Oct 2026 | Apache-2.0 | Tests only; the rules are plain C# over its model |
| Immediate.Handlers | 0.29 million | 9 contributors | 4.2.0, Sep 2026 | MIT | Mediator 3.x, or a small in-house dispatcher; handlers touch the package only through `[Handler]` and `IHandler<,>` |
| OpenTelemetry.* | 508 million | OpenTelemetry (CNCF) | 1.20.1, Oct 2026 | Apache-2.0 | The standard; exporting is OTLP, so any backend works |
| Microsoft.Extensions.Http.Resilience | 180 million | Microsoft | 10.11.0, Oct 2026 | MIT | Built on Polly; or Polly directly |
| Microsoft.Extensions.Diagnostics.Testing, OpenTelemetry.Exporter.InMemory | — | Microsoft, OpenTelemetry | 10.11.0, 1.20.1 | MIT, Apache-2.0 | Tests only |
| NetEscapades.AspNetCore.SecurityHeaders | 20 million | Andrew Lock | 1.3.1, **Dec 2025** (repository active, Jul 2026) | MIT | The API preset is a dozen headers set in `OnStarting`: a small middleware |
| Testcontainers, Testcontainers.Keycloak | 138 million, 1.2 million | Testcontainers (Docker) | 4.16.0, Oct 2026 | MIT | Tests only; `docker run` in a fixture |
| Microsoft.Extensions.TimeProvider.Testing | — | Microsoft | 10.11.0, Oct 2026 | MIT | Tests only |
| Scalar.AspNetCore | 56 million | Scalar (company), 260+ contributors | 2.18.3, Oct 2026 | MIT | Development only; the document is the build's file, so any viewer (or none) works |
| JetBrains.Annotations | 515 million | JetBrains | 2026.2.0, Jun 2026 | MIT | Compile-time only (`PrivateAssets=all`); delete the attributes |
| coverlet.MTP | — | coverlet | 10.1.0, Sep 2026 | MIT | Tests only |
| nuget-license (tool) | 2.2 million | sensslen | 4.0.18, Sep 2026 | Apache-2.0 | Tool only |
| JetBrains InspectCode (tool) | 15.8 million | JetBrains | 2026.2.3.1 | Free JetBrains licence (not open source) | Tool only; drop the step |
