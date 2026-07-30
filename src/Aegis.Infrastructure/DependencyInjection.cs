using Aegis.Core.Configuration;
using Aegis.Core.Interfaces;
using Aegis.Infrastructure.Commitment;
using Aegis.Infrastructure.Dns;
using Aegis.Infrastructure.Health;
using Aegis.Infrastructure.Rules;
using Aegis.Infrastructure.Security;
using Aegis.Infrastructure.Storage;
using Aegis.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAegisInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Configuration options binding
        services.Configure<ServiceOptions>(configuration.GetSection(ServiceOptions.SectionName));
        services.Configure<DnsOptions>(configuration.GetSection(DnsOptions.SectionName));
        services.Configure<FilteringOptions>(configuration.GetSection(FilteringOptions.SectionName));

        // 2. Storage & Migrator
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<SqliteStorageService>();
        services.AddSingleton<IStorageService>(sp => sp.GetRequiredService<SqliteStorageService>());

        // 3. Time & Security
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<ISecurityService, SecurityService>();

        // 4. Repositories
        services.AddSingleton<IModuleHealthRepository, ModuleHealthRepository>();
        services.AddSingleton<IBlocklistRepository, BlocklistRepository>();
        services.AddSingleton<IEventRepository, EventRepository>();

        // 5. Health Reporter
        services.AddSingleton<IHealthReporter, HealthReporter>();

        // 6. Filtering & Rule Engine
        services.AddSingleton<IRegexEngine, RegexEngine>();
        services.AddSingleton<IKeywordEngine, KeywordEngine>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IDnsFilter, DnsFilter>();

        // 7. Commitment Lock Engine
        services.AddSingleton<ICommitLockEngine, CommitLockEngine>();

        return services;
    }
}
