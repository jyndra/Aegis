using Aegis.Core.Configuration;
using Aegis.Infrastructure.Dns;
using Aegis.Infrastructure.Storage;
using Aegis.Infrastructure.Time;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class DnsFilterTests : IDisposable
{
    private readonly SqliteStorageService _storageService;
    private readonly BlocklistRepository _blocklistRepo;
    private readonly EventRepository _eventRepo;
    private readonly DnsFilter _dnsFilter;

    public DnsFilterTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_dns_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _blocklistRepo = new BlocklistRepository(_storageService, NullLogger<BlocklistRepository>.Instance);
        _eventRepo = new EventRepository(_storageService, NullLogger<EventRepository>.Instance);
        var timeProvider = new SystemTimeProvider();

        var dnsOpts = Options.Create(new DnsOptions { Enabled = true, ListenPort = 15353, ListenAddress = "127.0.0.1" });
        var filterOpts = Options.Create(new FilteringOptions());

        _dnsFilter = new DnsFilter(_blocklistRepo, _eventRepo, timeProvider, dnsOpts, filterOpts, NullLogger<DnsFilter>.Instance);
    }

    [Fact]
    public async Task IsDomainBlockedAsync_ReturnsTrue_ForBlockedDomain()
    {
        await _blocklistRepo.AddDomainAsync("badsite.com", "unit-test");
        await _dnsFilter.ReloadBlocklistAsync();

        bool isBlocked = await _dnsFilter.IsDomainBlockedAsync("www.badsite.com");
        bool isAllowed = await _dnsFilter.IsDomainBlockedAsync("goodsite.com");

        isBlocked.Should().BeTrue();
        isAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task StartAndStopAsync_ControlsListenerLifecycle()
    {
        await _dnsFilter.StartAsync();
        _dnsFilter.IsRunning.Should().BeTrue();

        await _dnsFilter.StopAsync();
        _dnsFilter.IsRunning.Should().BeFalse();
    }

    public void Dispose()
    {
        _dnsFilter.StopAsync().GetAwaiter().GetResult();
        if (File.Exists(_storageService.DbPath))
        {
            try { File.Delete(_storageService.DbPath); } catch { }
        }
    }
}
