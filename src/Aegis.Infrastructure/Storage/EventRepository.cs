using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Storage;

public class EventRepository : IEventRepository
{
    private readonly SqliteStorageService _storageService;
    private readonly ILogger<EventRepository> _logger;

    public EventRepository(SqliteStorageService storageService, ILogger<EventRepository> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task AddEventAsync(AegisEvent aegisEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO events (timestamp, component, event_type, severity, message, details_json)
                VALUES ($timestamp, $component, $event_type, $severity, $message, $details_json);
            ";

            cmd.Parameters.AddWithValue("$timestamp", aegisEvent.Timestamp.ToString("o"));
            cmd.Parameters.AddWithValue("$component", aegisEvent.Component);
            cmd.Parameters.AddWithValue("$event_type", aegisEvent.EventType);
            cmd.Parameters.AddWithValue("$severity", aegisEvent.Severity.ToString());
            cmd.Parameters.AddWithValue("$message", aegisEvent.Message);
            cmd.Parameters.AddWithValue("$details_json", aegisEvent.DetailsJson ?? "{}");

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Aegis event for component {Component}", aegisEvent.Component);
        }
    }

    public async Task<IReadOnlyList<AegisEvent>> GetRecentEventsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var eventsList = new List<AegisEvent>();
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, timestamp, component, event_type, severity, message, details_json
                FROM events
                ORDER BY id DESC
                LIMIT $count;
            ";
            cmd.Parameters.AddWithValue("$count", count);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                eventsList.Add(new AegisEvent(
                    Id: reader.GetInt64(0),
                    Timestamp: DateTimeOffset.Parse(reader.GetString(1)),
                    Component: reader.GetString(2),
                    EventType: reader.GetString(3),
                    Severity: Enum.Parse<FilterSeverity>(reader.GetString(4), ignoreCase: true),
                    Message: reader.GetString(5),
                    DetailsJson: reader.IsDBNull(6) ? "{}" : reader.GetString(6)
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query recent Aegis events");
        }

        return eventsList;
    }

    public async Task PurgeExpiredEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _storageService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var cutoff = DateTimeOffset.UtcNow.AddDays(-30).ToString("o");

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM events WHERE timestamp < $cutoff AND severity = 'Info';";
            cmd.Parameters.AddWithValue("$cutoff", cutoff);

            int deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Purged {Count} expired info events older than 30 days", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge expired events");
        }
    }
}
