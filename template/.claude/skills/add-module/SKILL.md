---
name: add-module
description: Add a backend module (a bounded context such as Billing or Catalog) in the six projects every module has, registered and tested. Use when a feature needs its own module rather than a slice in an existing one.
---

# Add a backend module

1. Pick a PascalCase name for the business capability (`Billing`, not `BillingService`). Check `backend/src/Modules/` has no module that already owns it: a new module is a boundary other modules can only cross through its `IntegrationEvents` and `PublicApi`.
2. Run `make module NAME=<Name>`. It creates `backend/src/Modules/<Name>/` (Domain, Application, Infrastructure, Presentation, IntegrationEvents, PublicApi) and `backend/tests/Modules.<Name>.UnitTests/` from `backend/templates/module`, adds them to `Backend.slnx`, references the module from the host, registers it in `Program.cs`, updates the lock files, and rewrites `backend/openapi/v1.json` with the example endpoint.
3. Replace the example `GetSampleStatus`-style query, its endpoint (`v1/<name>/status`) and its test with the module's first real use case. Keep handlers `internal`; give each request a FluentValidation validator when it has input.
4. Add the module's acceptance scenarios to `backend/tests/AcceptanceTests/Features/<Name>.feature`.
5. Run `make check` and commit `backend/openapi/v1.json` with the code (`make check` fails when it is stale; `make openapi-backend` rewrites it). The architecture tests (`backend/tests/ArchitectureTests`) fail if a step was missed: a project not in the solution, the module not registered, a missing `[assembly: Behaviors(...)]`, or a dependency on another module's internals.

Don't edit `backend/templates/module` for one module's needs; change it only when every future module should start differently.
