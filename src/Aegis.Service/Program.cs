using Aegis.Infrastructure;
using Aegis.Service;
using Aegis.Service.Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:9443", "http://127.0.0.1:5000");

// Configure Serilog from appsettings.json
builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

if (Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
{
    builder.Host.UseWindowsService();
}

// Add Infrastructure dependencies (configuration binding is inside AddAegisInfrastructure)
builder.Services.AddAegisInfrastructure(builder.Configuration);

// Configure Minimal APIs to serialize enums as strings and enable CORS
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Register background loops
builder.Services.AddHostedService<AegisBackgroundService>();

var app = builder.Build();

app.UseCors();

// Map API endpoints
app.MapHealthEndpoints();
app.MapPolicyEndpoints();
app.MapHandshakeEndpoints();
app.MapEvaluateEndpoints();
app.MapUnlockEndpoints();
app.MapIntegrityEndpoints();
app.MapStatusEndpoints();
app.MapDeploymentEndpoints();
app.MapDashboardEndpoints();

app.Run();

public partial class Program { }
