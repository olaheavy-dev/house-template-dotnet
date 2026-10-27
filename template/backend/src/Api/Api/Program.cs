using Carter;
using Common.Infrastructure;
using Modules.Platform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCommon();
builder.Services.AddPlatformModule(builder.Configuration);
// Each module's Presentation assembly, where its endpoints live; a new module adds its own. Pass an
// array: one bare assembly selects the overload that scans that assembly's dependencies instead.
builder.Services.AddCarter(new DependencyContextAssemblyCatalog(
[
    Modules.Platform.Presentation.AssemblyReference.Assembly,
]));

var app = builder.Build();

app.MapCarter();

await app.RunAsync();
