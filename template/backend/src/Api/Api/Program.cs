using Carter;
using Common.Infrastructure;
using Modules.Platform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCommon();
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

app.MapCarter();

await app.RunAsync();
