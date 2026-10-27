using Api;
using Carter;
using Common.Infrastructure;
using Common.Presentation;
using Modules.Platform.Infrastructure;
using Scalar.AspNetCore;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddCommon();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiOpenApi(builder.Configuration);
builder.Services.AddHardening(builder.Configuration);
// Each module registers itself. `make module NAME=<Name>` adds new ones above the marker.
builder.Services.AddPlatformModule(builder.Configuration);
// make module: registrations

// Each module's Presentation assembly, where its endpoints live. Pass an array: one bare assembly
// selects the overload that scans that assembly's dependencies instead.
builder.Services.AddCarter(new DependencyContextAssemblyCatalog(
[
    Modules.Platform.Presentation.AssemblyReference.Assembly,
    // make module: endpoints
]));

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
app.MapCarter();

await app.RunAsync();
