using Microsoft.AspNetCore.DataProtection;
using PandaBear.Testing.AspNetCore.TestApp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Greeter("hello from the app"));
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();

app.UseAntiforgery();

app.MapGet("/ping", (IConfiguration configuration, IHostEnvironment environment, Greeter greeter) =>
    $"{environment.EnvironmentName}|{configuration["Setting"]}|{greeter.Text}");

app.MapRazorComponents<StateDocument>().AddInteractiveServerRenderMode();

await app.RunAsync();
