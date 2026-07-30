using Aegis.Core.Interfaces;
using Aegis.Infrastructure.CommitLock;
using Aegis.Infrastructure.Configuration;
using Aegis.Infrastructure.Dns;
using Aegis.Infrastructure.Health;
using Aegis.Infrastructure.Integrity;
using Aegis.Infrastructure.Rules;
using Aegis.Infrastructure.Security;
using Aegis.Infrastructure.Storage;
using Aegis.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAegisInfrastructure(this IServiceCollection services)
    {
        // Time & Configuration
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<IConfigValidator, ConfigValidator>();

        // Security & Cryptography
        services.AddSingleton<ISecurityService, SecurityService>();

        // Core Engines
        services.AddSingleton<IDnsFilter, DnsFilter>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IKeywordEngine, KeywordEngine>();
        services.AddSingleton<IRegexEngine, RegexEngine>();
        services.AddSingleton<IIntegrityEngine, IntegrityEngine>();
        services.AddSingleton<ICommitLockEngine, CommitLockEngine>();

        // Storage & Repositories
        services.AddSingleton<IStorageService, SqliteStorageService>();
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
        services.AddSingleton<IBlocklistRepository, BlocklistRepository>();
        services.AddSingleton<IEventRepository, EventRepository>();
        services.AddSingleton<ILockStateRepository, LockStateRepository>();
        services.AddSingleton<IModuleHealthRepository, ModuleHealthRepository>();
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<IIntegrityRepository, IntegrityRepository>();

        // Health Monitoring
        services.AddSingleton<IHealthReporter, HealthReporter>();

        return services;
    }
}
