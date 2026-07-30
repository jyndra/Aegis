using System.Net;
using System.Net.Sockets;
using Aegis.Core.Configuration;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Aegis.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Dns;

public class DnsFilter : IDnsFilter
{
    private readonly IBlocklistRepository _blocklistRepo;
    private readonly IEventRepository _eventRepo;
    private readonly ITimeProvider _timeProvider;
    private readonly IOptions<DnsOptions> _dnsOptions;
    private readonly IOptions<FilteringOptions> _filteringOptions;
    private readonly ILogger<DnsFilter> _logger;

    private UdpClient? _udpListener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private HashSet<string> _inMemoryBlocklist = new(StringComparer.OrdinalIgnoreCase);

    public DnsFilter(
        IBlocklistRepository blocklistRepo,
        IEventRepository eventRepo,
        ITimeProvider timeProvider,
        IOptions<DnsOptions> dnsOptions,
        IOptions<FilteringOptions> filteringOptions,
        ILogger<DnsFilter> logger)
    {
        _blocklistRepo = blocklistRepo;
        _eventRepo = eventRepo;
        _timeProvider = timeProvider;
        _dnsOptions = dnsOptions;
        _filteringOptions = filteringOptions;
        _logger = logger;
    }

    public bool IsRunning => _udpListener != null && _listenerTask != null && !_listenerTask.IsCompleted;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_dnsOptions.Value.Enabled)
        {
            _logger.LogInformation("DNS filtering is disabled in configuration.");
            return;
        }

        if (IsRunning)
        {
            _logger.LogWarning("DNS filter listener is already running.");
            return;
        }

        await ReloadBlocklistAsync(cancellationToken);

        int port = _dnsOptions.Value.ListenPort;
        string bindIp = _dnsOptions.Value.ListenAddress;

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(bindIp), port);
            _udpListener = new UdpClient(endpoint);
            _cts = new CancellationTokenSource();

            _logger.LogInformation("DNS Proxy listening on {Address}:{Port}...", bindIp, port);

            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token), cancellationToken);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to bind DNS UDP listener on {Address}:{Port} ({ErrorCode}). Elevation or port fallback required.", bindIp, port, AegisErrorCodes.DnsBindFailed);
            _udpListener = null;
            // Record bind error event
            await _eventRepo.AddEventAsync(new AegisEvent(
                Id: 0,
                Timestamp: _timeProvider.UtcNow,
                Component: "DNS",
                EventType: "DnsBindFailed",
                Severity: FilterSeverity.Warning,
                Message: $"Failed to bind UDP port {port}: {ex.Message}",
                DetailsJson: $"{{\"errorCode\": \"{AegisErrorCodes.DnsBindFailed}\", \"port\": {port}}}"
            ), cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_udpListener != null)
        {
            _udpListener.Close();
            _udpListener = null;
        }

        if (_listenerTask != null)
        {
            try { await _listenerTask; } catch { }
            _listenerTask = null;
        }

        _logger.LogInformation("DNS Proxy listener stopped.");
    }

    public Task<bool> IsDomainBlockedAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain)) return Task.FromResult(false);

        string norm = BlocklistRepository.NormalizeDomain(domain);
        bool blocked = _inMemoryBlocklist.Contains(norm);

        return Task.FromResult(blocked);
    }

    public async Task ReloadBlocklistAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading in-memory DNS blocklist...");

        var newBlocklist = await _blocklistRepo.GetAllDomainHashesAsync(cancellationToken);

        // Also load custom blacklist file if configured
        string customFile = _filteringOptions.Value.CustomBlacklistPath;
        if (File.Exists(customFile))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(customFile, cancellationToken);
                foreach (var line in lines)
                {
                    string norm = BlocklistRepository.NormalizeDomain(line);
                    if (!string.IsNullOrEmpty(norm) && !norm.StartsWith('#'))
                    {
                        newBlocklist.Add(norm);
                    }
                }
                _logger.LogInformation("Loaded custom domain rules from {Path}", customFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read custom blacklist file at {Path}", customFile);
            }
        }

        _inMemoryBlocklist = newBlocklist;
        _logger.LogInformation("In-memory DNS blocklist ready with {Count} domains.", _inMemoryBlocklist.Count);
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udpListener != null)
        {
            try
            {
                var result = await _udpListener.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => ProcessDnsQueryAsync(result.Buffer, result.RemoteEndPoint, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving DNS UDP packet");
            }
        }
    }

    private async Task ProcessDnsQueryAsync(byte[] queryBuffer, IPEndPoint clientEndPoint, CancellationToken cancellationToken)
    {
        try
        {
            var packet = DnsPacket.Parse(queryBuffer);
            string qdomain = packet.Questions.FirstOrDefault()?.Domain ?? string.Empty;

            bool isBlocked = await IsDomainBlockedAsync(qdomain, cancellationToken);

            if (isBlocked)
            {
                _logger.LogInformation("DNS BLOCK: {Domain} requested by {Client}", qdomain, clientEndPoint);

                byte[] responseBuffer = DnsPacket.BuildBlockResponse(queryBuffer);
                if (_udpListener != null && responseBuffer.Length > 0)
                {
                    await _udpListener.SendAsync(responseBuffer, responseBuffer.Length, clientEndPoint);
                }

                await _eventRepo.AddEventAsync(new AegisEvent(
                    Id: 0,
                    Timestamp: _timeProvider.UtcNow,
                    Component: "DNS",
                    EventType: "DnsBlock",
                    Severity: FilterSeverity.Warning,
                    Message: $"Blocked DNS query for {qdomain}",
                    DetailsJson: $"{{\"domain\": \"{qdomain}\", \"client\": \"{clientEndPoint}\"}}"
                ), cancellationToken);
            }
            else
            {
                byte[]? responseBuffer = await ForwardUpstreamAsync(queryBuffer, cancellationToken);
                if (responseBuffer != null && _udpListener != null)
                {
                    await _udpListener.SendAsync(responseBuffer, responseBuffer.Length, clientEndPoint);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing DNS query packet from {Client}", clientEndPoint);
        }
    }

    private async Task<byte[]?> ForwardUpstreamAsync(byte[] queryBuffer, CancellationToken cancellationToken)
    {
        var upstreams = _dnsOptions.Value.UpstreamServers;
        string upstreamIp = upstreams.FirstOrDefault() ?? "1.1.1.1";

        try
        {
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 2000;
            client.Client.SendTimeout = 2000;

            var target = new IPEndPoint(IPAddress.Parse(upstreamIp), 53);
            await client.SendAsync(queryBuffer, queryBuffer.Length, target);

            using var ctsWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ctsWithTimeout.CancelAfter(TimeSpan.FromSeconds(2));

            var result = await client.ReceiveAsync(ctsWithTimeout.Token);
            return result.Buffer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upstream DNS forwarding to {Upstream} failed or timed out", upstreamIp);
            return null;
        }
    }
}
