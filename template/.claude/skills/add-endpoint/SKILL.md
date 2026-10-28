---
name: add-endpoint
description: Add an API endpoint to a backend module: handler, validator, store method, route, permission, tests and the OpenAPI document. Use for any new route (a read, a write, a list) in an existing module; use add-module first when the feature needs a module of its own.
---

# Add an endpoint

`Modules.Platform` has the worked examples: `Notes` (create, get, list), `Flags`, `Me`. Copy the nearest one and follow the steps; the architecture tests fail if a step is missed or a layer reaches too far.

1. **Shape it.** A command changes data (`POST`, `PUT`, `DELETE`), a query reads (`GET`). Name the use case `<Verb><Thing>` (`CreateNote`), the route `v1/<module>/<things>` (lower-case, plural, ids in the path as `{id:guid}`), and decide who may call it: a signed-in user with one permission, and whose data it touches (scope the query to the signed-in user; answer 404, never 403, for someone else's thing).
2. **Domain** (`Modules.<M>.Domain`, only if the rule belongs to the entity). A method that changes state returns `ErrorOr<Success>` and does its own validation (`Note.Edit`). The domain knows nothing else: no EF Core, no `TimeProvider`, no HTTP.
3. **Application** (`Modules.<M>.Application/<Things>/`):
   - A public request record and a public response record (`CreateNoteCommand`, `NoteResponse`). Ids are generated on the server (`Guid.CreateVersion7(clock.GetUtcNow())`); time comes from `TimeProvider`.
   - An `internal` `AbstractValidator<TRequest>` for input the handler would otherwise have to check (a list query adds `RuleFor(q => q.Page).SetValidator(new PageRequestValidator())`).
   - An `[Handler] internal sealed partial class` with `private async ValueTask<ErrorOr<TResponse>> HandleAsync(TRequest, CancellationToken)`. Expected failures are `Error.NotFound("note.not_found", "...")` and friends with stable `area.reason` codes, never exceptions.
   - Data goes through the module's store interface in this project (`INoteStore`: add the method there). To tell other modules something happened: `await store.PublishAsync(new NoteCreated(...), ct)` then `await store.SaveChangesAsync(ct)`; the event type lives in `Modules.<M>.IntegrationEvents` and a subscriber is a public `*Consumer` class (`make codegen` afterwards).
   - A read worth caching goes through `HybridCache` with a key that names the thing and a lookup on a singleton reader (`INoteReader`); a handler that changes it calls `cache.RemoveAsync(key)` after saving, with the key built in one place (`NoteCache.KeyFor`) so the reader and the writer cannot disagree; add a scenario that reads the thing, changes it, and reads it again. A list is paged by cursor (`Cursor.Decode`, `Page.From`), never by offset.
4. **Infrastructure** (`Modules.<M>.Infrastructure/Persistence/`): implement the new store method in `<Thing>Store`. A new entity, column or index: change `<M>DbContext`, then `dotnet ef migrations add <Name> --project backend/src/Modules/<M>/Modules.<M>.Infrastructure --output-dir Persistence/Migrations` and commit the migration. Index the filter and the order of any list you add.
5. **Presentation** (`Modules.<M>.Presentation`):
   - `<Feature>Endpoint.cs`: `internal static class` with `internal static void Map<Feature>(this IEndpointRouteBuilder app)`, a route under `v1/`, the handler as `IHandler<TRequest, ErrorOr<TResponse>>`, and `ApiResults.Ok/Created/NoContent(result)`.
   - Chain: `.RequirePermission(<M>Permissions.X)`, `.WithName`, `.WithTags`, `.Produces<T>()`, `.ProducesProblem(...)` for 401, 403 and the errors the handler returns. A retryable `POST` adds `.WithIdempotencyKey()`. An endpoint behind a feature flag ends the chain with `.WithFeatureGate(<M>Flags.X)` (it returns the plain builder, so nothing can follow).
   - A new permission: add the constant to `<M>Permissions` and, in `backend/keycloak/realm.json`, a client role under `roles.client.api` and the name in the default role's composite list (`composites.client.api`), and update the permission list `KeycloakTests` expects. Call the new `Map<Feature>()` from `<M>Endpoints.cs`; the architecture test fails if you forget.
6. **Tests.** Write them first and watch them fail.
   - Unit (`tests/Modules.<M>.UnitTests`): the handler through the module's own registration with an in-memory store (add the new store method to the in-memory store, and to any other `INoteStore`-style fake there, or the project stops compiling), one test per branch (success, validation, not found, someone else's thing).
   - Acceptance (`tests/AcceptanceTests/Features/<M>.feature` and `Steps/`): a scenario in the product's words for each thing a user can do, driving the API as `X-Dev-User: <id>`.
   - Integration (`tests/IntegrationTests`) only for HTTP details the scenarios do not show.
   - Then break the new code once (flip a comparison, drop the owner check) and see exactly its test fail.
7. **Regenerate and check.** `make openapi-backend` and commit `backend/openapi/v1.json`; `make codegen` if you added a subscriber; `make migrate` to bring a local database up; `make check`.

Do not: use `DbContext` or anything from EF Core outside Infrastructure; return entities from an endpoint; log request bodies, tokens or personal data; read the clock, the environment or the identity provider directly; hand-edit generated code (`Internal/Generated`, `Persistence/Migrations`, `openapi/v1.json`).
