using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class BlocklistRepository : IBlocklistRepository
{
    private readonly SqliteStorageService _storageService;
    private readonly ILogger<BlocklistRepository> _logger;

    public BlocklistRepository(SqliteStorageService storageService, ILogger<BlocklistRepository> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public static string ComputeDomainHash(string domain)
    {
        string normalized = NormalizeDomain(domain);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return string.Empty;

        string norm = domain.Trim().ToLowerInvariant();
        
        // Strip URI schemes if present
        if (norm.StartsWith("https://")) norm = norm[8..];
        else if (norm.StartsWith("http://")) norm = norm[7..];

        // Strip path, query string, or trailing slash
        int slashIdx = norm.IndexOf('/');
        if (slashIdx >= 0) norm = norm[..slashIdx];

        // Strip port number if present
        int colonIdx = norm.IndexOf(':');
        if (colonIdx >= 0) norm = norm[..colonIdx];

        if (norm.StartsWith("www."))
        {
            norm = norm[4..];
        }
        return norm.TrimEnd('.');
    }

    public async Task AddDomainAsync(string domain, string source, CancellationToken cancellationToken = default)
    {
        string norm = NormalizeDomain(domain);
        if (string.IsNullOrEmpty(norm)) return;

        string hash = ComputeDomainHash(norm);

        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO domain_blocklist (domain_hash, domain, source, imported_at)
            VALUES ($hash, $domain, $source, $imported_at);
        ";
        AddParameter(cmd, "$hash", hash);
        AddParameter(cmd, "$domain", norm);
        AddParameter(cmd, "$source", source);
        AddParameter(cmd, "$imported_at", DateTimeOffset.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task BulkAddDomainsAsync(IEnumerable<string> domains, string source, CancellationToken cancellationToken = default)
    {
        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        string nowStr = DateTimeOffset.UtcNow.ToString("o");
        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT OR IGNORE INTO domain_blocklist (domain_hash, domain, source, imported_at)
            VALUES ($hash, $domain, $source, $imported_at);
        ";

        AddParameter(cmd, "$hash", "");
        AddParameter(cmd, "$domain", "");
        AddParameter(cmd, "$source", source);
        AddParameter(cmd, "$imported_at", nowStr);

        int count = 0;
        foreach (var domain in domains)
        {
            string norm = NormalizeDomain(domain);
            if (string.IsNullOrEmpty(norm)) continue;

            cmd.Parameters["$hash"].Value = ComputeDomainHash(norm);
            cmd.Parameters["$domain"].Value = norm;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            count++;
        }

        transaction.Commit();
        _logger.LogInformation("Bulk added {Count} domains to blocklist from source '{Source}'", count, source);
    }

    public async Task<bool> ContainsDomainHashAsync(string domainHash, CancellationToken cancellationToken = default)
    {
        using var connection = _storageService.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM domain_blocklist WHERE domain_hash = $hash LIMIT 1;";
        AddParameter(cmd, "$hash", domainHash);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    public async Task<HashSet<string>> GetAllDomainHashesAsync(CancellationToken cancellationToken = default)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT domain FROM domain_blocklist;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                hashes.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading all domains from blocklist");
        }

        return hashes;
    }

    public async Task<IReadOnlyList<BlockedRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = new List<BlockedRule>();
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, rule_type, pattern, enabled, source, weight, created_at FROM blocked_rules WHERE enabled = 1;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rules.Add(new BlockedRule(
                    Id: reader.GetInt64(0),
                    RuleType: reader.GetString(1),
                    Pattern: reader.GetString(2),
                    Enabled: reader.GetInt32(3) == 1,
                    Source: reader.GetString(4),
                    Weight: reader.GetInt32(5),
                    CreatedAt: DateTimeOffset.Parse(reader.GetString(6))
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading blocked rules");
        }

        return rules;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }
}
