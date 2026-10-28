using Api;
using Common.Infrastructure;
using Common.Presentation;
using Modules.Platform.Infrastructure;
using Modules.Platform.Presentation;
using Scalar.AspNetCore;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddCommon();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiOpenApi(builder.Configuration);
builder.Services.AddHardening(builder.Configuration);
builder.Services.AddAuth(builder.Configuration, builder.Environment);
// Each module registers itself. `make module NAME=<Name>` adds new ones above the marker.
builder.Services.AddPlatformModule(builder.Configuration);
// make module: registrations

var app = builder.Build();

app.UseServiceDefaults();
app.UseSecurityHeaders();
// An unhandled exception, and a status code with no body, become problem details.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHardening();

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

await app.RunAsync();
