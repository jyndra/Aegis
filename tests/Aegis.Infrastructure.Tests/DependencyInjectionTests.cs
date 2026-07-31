using Aegis.Core.Interfaces;
using Aegis.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAegisInfrastructure_RegistersAllDomainInterfaces()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAegisInfrastructure(config);

        var provider = services.BuildServiceProvider();

        provider.GetService<ITimeProvider>().Should().NotBeNull();
        provider.GetService<ISecurityService>().Should().NotBeNull();
        provider.GetService<IDnsFilter>().Should().NotBeNull();
        provider.GetService<IRuleEngine>().Should().NotBeNull();
        provider.GetService<IIntegrityEngine>().Should().NotBeNull();
        provider.GetService<ICommitLockEngine>().Should().NotBeNull();
        provider.GetService<IStorageService>().Should().NotBeNull();
        provider.GetService<IHealthReporter>().Should().NotBeNull();
    }
}
