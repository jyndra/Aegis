using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class ModuleHealthRepository : IModuleHealthRepository
{
    private readonly SqliteStorageService _storageService;
    private readonly ILogger<ModuleHealthRepository> _logger;

    public ModuleHealthRepository(SqliteStorageService storageService, ILogger<ModuleHealthRepository> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task SaveHealthReportAsync(HealthReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO module_health (component, status, last_checked_at, detail_json)
                VALUES ($component, $status, $last_checked_at, $detail_json)
                ON CONFLICT(component) DO UPDATE SET
                    status = excluded.status,
                    last_checked_at = excluded.last_checked_at,
                    detail_json = excluded.detail_json;
            ";

            cmd.Parameters.AddWithValue("$component", report.Component);
            cmd.Parameters.AddWithValue("$status", report.Status);
            cmd.Parameters.AddWithValue("$last_checked_at", report.LastCheckedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$detail_json", report.DetailJson ?? "{}");

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save health report for component {Component}", report.Component);
        }
    }

    public async Task<IReadOnlyList<HealthReport>> GetAllHealthReportsAsync(CancellationToken cancellationToken = default)
    {
        var reports = new List<HealthReport>();
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT component, status, last_checked_at, detail_json FROM module_health;";

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string component = reader.GetString(0);
                string status = reader.GetString(1);
                DateTimeOffset lastCheckedAt = DateTimeOffset.Parse(reader.GetString(2));
                string detailJson = reader.IsDBNull(3) ? "{}" : reader.GetString(3);

                reports.Add(new HealthReport(component, status, lastCheckedAt, detailJson));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch module health reports");
        }

        return reports;
    }
}
