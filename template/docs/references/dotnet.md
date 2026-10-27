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
