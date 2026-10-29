using Api;
using Common.Infrastructure;
using Common.Infrastructure.Caching;
using Common.Infrastructure.Flags;
using Common.Infrastructure.Idempotency;
using Common.Infrastructure.Messaging;
using Common.Infrastructure.Persistence;
using Common.Presentation;
using Common.Presentation.Idempotency;
using JasperFx;
using Modules.Platform.Infrastructure;
using Modules.Platform.Presentation;
using Scalar.AspNetCore;
using ServiceDefaults;

// `Api.dll healthcheck` is the image's HEALTHCHECK: it needs nothing but the running API.
if (args is ["healthcheck"])
{
    return await HealthProbe.RunAsync(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
}

var builder = WebApplication.CreateBuilder(args);

// `--role=worker` (or ROLE=worker) runs this image as a worker: the same modules, messaging and telemetry,
// but no API surface (no auth, OpenAPI, hardening or module endpoints), only /health/* for the orchestrator.
var isWorker = AppRole.IsWorker(builder.Configuration);

builder.AddServiceDefaults();
builder.AddMessaging(typeof(Program).Assembly);
builder.Services.AddCommon();
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddFeatureFlags(builder.Configuration);
if (!isWorker)
{
    builder.Services.AddIdempotency(builder.Configuration);
    builder.Services.AddApiProblemDetails();
    builder.Services.AddApiOpenApi(builder.Configuration);
    builder.Services.AddHardening(builder.Configuration);
    builder.Services.AddAuth(builder.Configuration, builder.Environment);
}

// Each module registers itself. `make module NAME=<Name>` adds new ones above the marker.
builder.Services.AddPlatformModule(builder.Configuration);
// make module: registrations

var app = builder.Build();

// `Api.dll migrate` brings the database up to date (every module's tables and the messaging tables) and exits:
// a deploy runs it once, from this same image, before the new version starts.
if (args is ["migrate"])
{
    await app.Services.MigrateDatabaseAsync();
    return 0;
}

// `make seed` runs the modules' seeders, development data only, and exits.
if (args is ["seed"])
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Seeding is for Development only.");
    }

    await app.Services.RunSeedersAsync(CancellationToken.None);
    return 0;
}

app.UseServiceDefaults();

if (isWorker)
{
    // Messaging runs as hosted services of this host: it recovers and processes the messages the outbox and
    // the durable queues hold. Only the health endpoints answer, so the image's HEALTHCHECK works unchanged.
    app.MapHealthEndpoints();
    await app.RunAsync();
    return 0;
}

app.UseSecurityHeaders();
// An unhandled exception, and a status code with no body, become problem details.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHardening();
// Endpoints that opt in with .WithIdempotencyKey() run once per Idempotency-Key.
app.UseIdempotency();

if (app.Environment.IsDevelopment())
{
    // The live document and the Scalar reference (/scalar). Clients use the one the build writes.
    app.MapOpenApi();
    app.MapScalarApiReference().WithSecurityHeadersPolicy(Hardening.DocsPolicy);
}

app.MapHealthEndpoints();
// Each module maps its own endpoints. `make module` adds new ones above the marker.
app.MapPlatformEndpoints();
// make module: endpoints

// `dotnet run -- codegen write` (make codegen) and the other messaging commands; anything else runs the API.
if (args is ["codegen" or "resources" or "describe", ..])
{
    return await app.RunJasperFxCommands(args);
}

await app.RunAsync();
return 0;
