# .NET references

Owned by house-template-dotnet. The versions this backend pins, with the official docs for each. Check these (or the Microsoft Learn MCP server in `.mcp.json`) before using an API; update the table when Dependabot moves a version.

| What | Pinned | Docs |
|---|---|---|
| .NET SDK and runtime | SDK 10.0.403, runtime 10.0.14 (`global.json`) | https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview |
| ASP.NET Core minimal APIs | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis |
| Problem Details (RFC 9457) | 10.0 | https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api |
| Carter | 10.0.0 | https://github.com/CarterCommunity/Carter |
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
| Mono.Cecil | 116 million | jbevain | 0.11.6, **Oct 2024** (accepted until Apr 2027) | MIT | Goes with ArchUnitNET |
| TngTech.ArchUnitNET | 10.6 million | TNG | 0.13.5, Oct 2026 | Apache-2.0 | Tests only; the rules are plain C# over its model |
| Carter | 5.1 million | 54 contributors | 10.0.0, **Nov 2025** | MIT | Each `ICarterModule` becomes a `MapGroup` extension method |
| Immediate.Handlers | 0.29 million | 9 contributors | 4.2.0, Sep 2026 | MIT | Mediator 3.x, or a small in-house dispatcher; handlers touch the package only through `[Handler]` and `IHandler<,>` |
| coverlet.MTP | — | coverlet | 10.1.0, Sep 2026 | MIT | Tests only |
| nuget-license (tool) | 2.2 million | sensslen | 4.0.18, Sep 2026 | Apache-2.0 | Tool only |
| JetBrains InspectCode (tool) | 15.8 million | JetBrains | 2026.2.3.1 | Free JetBrains licence (not open source) | Tool only; drop the step |
