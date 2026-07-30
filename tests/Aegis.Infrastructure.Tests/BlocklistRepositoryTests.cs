using Aegis.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class BlocklistRepositoryTests : IDisposable
{
    private readonly SqliteStorageService _storageService;
    private readonly BlocklistRepository _blocklistRepo;

    public BlocklistRepositoryTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"aegis_blocklist_test_{Guid.NewGuid():N}.db");
        var migrator = new DatabaseMigrator(NullLogger<DatabaseMigrator>.Instance);
        _storageService = new SqliteStorageService(NullLogger<SqliteStorageService>.Instance, migrator, tempDbPath);
        _storageService.InitializeDatabaseAsync().GetAwaiter().GetResult();

        _blocklistRepo = new BlocklistRepository(_storageService, NullLogger<BlocklistRepository>.Instance);
    }

    [Fact]
    public void NormalizeDomain_StripsWwwAndTrailingDots()
    {
        BlocklistRepository.NormalizeDomain("www.pornsite.com.").Should().Be("pornsite.com");
        BlocklistRepository.NormalizeDomain("  BADSITE.COM  ").Should().Be("badsite.com");
    }

    [Fact]
    public async Task BulkAddDomainsAsync_StoresDomainsInSqlite()
    {
        var domains = new[] { "porn1.com", "porn2.net", "www.porn3.org" };

        await _blocklistRepo.BulkAddDomainsAsync(domains, "test-source");

        var allDomains = await _blocklistRepo.GetAllDomainHashesAsync();
        allDomains.Should().HaveCount(3);
        allDomains.Should().Contain("porn1.com");
        allDomains.Should().Contain("porn2.net");
        allDomains.Should().Contain("porn3.org");
    }

    public void Dispose()
    {
        if (File.Exists(_storageService.DbPath))
        {
            try { File.Delete(_storageService.DbPath); } catch { }
        }
    }
}
