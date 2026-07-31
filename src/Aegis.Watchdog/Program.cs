using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aegis.Watchdog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AegisWatchdog";
});

builder.Services.AddHostedService<WatchdogBackgroundService>();

var host = builder.Build();
host.Run();
