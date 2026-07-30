using Aegis.Infrastructure;
using Aegis.Service;
using Aegis.Service.Api;
using Serilog;

// Configure Serilog first so even startup failures are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json (overrides bootstrap logger)
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                               "Aegis", "logs", "aegis-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

    builder.Host.UseWindowsService();

    // Add Infrastructure dependencies (configuration binding is inside AddAegisInfrastructure)
    builder.Services.AddAegisInfrastructure(builder.Configuration);

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
}
catch (Exception ex)
{
    // Global unhandled exception catch — ensures all startup failures are logged before process exits
    Log.Fatal(ex, "Aegis Service terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
