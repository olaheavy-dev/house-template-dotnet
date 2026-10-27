using Api;
using Carter;
using Common.Infrastructure;
using Common.Presentation;
using Modules.Platform.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCommon();
builder.Services.AddApiProblemDetails();
builder.Services.AddApiOpenApi(builder.Configuration);
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

// An unhandled exception, and a status code with no body, become problem details.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // The live document and the Scalar reference (/scalar). Clients use the one the build writes.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapCarter();

await app.RunAsync();
