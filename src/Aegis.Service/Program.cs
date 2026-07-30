using Aegis.Core.Configuration;
using Aegis.Infrastructure;
using Aegis.Service;
using Aegis.Service.Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Aegis", "logs", "aegis-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Host.UseWindowsService();

// Bind configuration options
builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection(ServiceOptions.SectionName));
builder.Services.Configure<DnsOptions>(builder.Configuration.GetSection(DnsOptions.SectionName));
builder.Services.Configure<FilteringOptions>(builder.Configuration.GetSection(FilteringOptions.SectionName));
builder.Services.Configure<LockOptions>(builder.Configuration.GetSection(LockOptions.SectionName));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.SectionName));

// Add Infrastructure dependencies
builder.Services.AddAegisInfrastructure();

// Register background loops
builder.Services.AddHostedService<AegisBackgroundService>();

var app = builder.Build();

// Map API endpoints
app.MapHealthEndpoints();
app.MapPolicyEndpoints();
app.MapHandshakeEndpoints();
app.MapEvaluateEndpoints();
app.MapUnlockEndpoints();
app.MapIntegrityEndpoints();
app.MapStatusEndpoints();
app.MapDeploymentEndpoints();

app.Run();

public partial class Program { }
