var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Greeter("hello from the app"));
var app = builder.Build();

app.MapGet("/ping", (IConfiguration configuration, IHostEnvironment environment, Greeter greeter) =>
    $"{environment.EnvironmentName}|{configuration["Setting"]}|{greeter.Text}");

await app.RunAsync();
