using Api;
using Common.Infrastructure;
using Common.Infrastructure.Caching;
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

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddMessaging(typeof(Program).Assembly);
builder.Services.AddCommon();
builder.Services.AddIdempotency(builder.Configuration);
builder.Services.AddCaching(builder.Configuration);
builder.Services.AddApiProblemDetails();
builder.Services.AddApiOpenApi(builder.Configuration);
builder.Services.AddHardening(builder.Configuration);
builder.Services.AddAuth(builder.Configuration, builder.Environment);
// Each module registers itself. `make module NAME=<Name>` adds new ones above the marker.
builder.Services.AddPlatformModule(builder.Configuration);
// make module: registrations

var app = builder.Build();

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
