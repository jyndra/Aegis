using System.Net;
using System.Net.Sockets;
using System.Text;
using Aegis.Core.Configuration;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Proxy;

public class ProxyServer : IProxyServer
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IEventRepository _eventRepo;
    private readonly ITimeProvider _timeProvider;
    private readonly IOptions<ProxyOptions> _proxyOptions;
    private readonly ILogger<ProxyServer> _logger;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    // Hardening M10: Concurrency gate — prevents connection flood DoS exhausting ThreadPool
    private readonly SemaphoreSlim _connectionGate = new(50, 50);

    public ProxyServer(
        IRuleEngine ruleEngine,
        IEventRepository eventRepo,
        ITimeProvider timeProvider,
        IOptions<ProxyOptions> proxyOptions,
        ILogger<ProxyServer> logger)
    {
        _ruleEngine = ruleEngine;
        _eventRepo = eventRepo;
        _timeProvider = timeProvider;
        _proxyOptions = proxyOptions;
        _logger = logger;
    }

    public bool IsRunning => _listener != null && _acceptTask != null && !_acceptTask.IsCompleted;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_proxyOptions.Value.Enabled)
        {
            _logger.LogInformation("Proxy server is disabled in configuration.");
            return;
        }

        if (IsRunning)
        {
            _logger.LogWarning("Proxy server is already running.");
            return;
        }

        int port = _proxyOptions.Value.ListenPort;
        string bindIp = _proxyOptions.Value.ListenAddress;

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(bindIp), port);
            _listener = new TcpListener(endpoint);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _logger.LogInformation("Aegis HTTP Proxy listening on {Address}:{Port} (Isolated dev port)...", bindIp, port);

            _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token), cancellationToken);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Failed to bind Proxy TCP listener on {Address}:{Port} ({ErrorCode}). Dev port fallback required.", bindIp, port, AegisErrorCodes.PortInUse);
            _listener = null;

            await _eventRepo.AddEventAsync(new AegisEvent(
                Id: 0,
                Timestamp: _timeProvider.UtcNow,
                Component: "Proxy",
                EventType: "ProxyPortBindFailed",
                Severity: FilterSeverity.Warning,
                Message: $"Failed to bind Proxy TCP port {port}: {ex.Message}",
                DetailsJson: $"{{\"errorCode\": \"{AegisErrorCodes.PortInUse}\", \"port\": {port}}}"
            ), cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_listener != null)
        {
            _listener.Stop();
            _listener = null;
        }

        if (_acceptTask != null)
        {
            try { await _acceptTask; } catch { }
            _acceptTask = null;
        }

        // Hardening M10: Dispose CTS to release native OS timer handle (was previously leaked)
        _cts?.Dispose();
        _cts = null;

        _logger.LogInformation("Aegis HTTP Proxy server stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);

                // Hardening M10: Concurrency gate — reject connection immediately if at capacity
                if (!await _connectionGate.WaitAsync(0, cancellationToken))
                {
                    _logger.LogWarning("Proxy connection gate full (50 concurrent). Rejecting connection from {Remote}.",
                        client.Client.RemoteEndPoint);
                    using var stream = client.GetStream();
                    byte[] busy = System.Text.Encoding.ASCII.GetBytes(
                        "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(busy, cancellationToken);
                    client.Close();
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try { await HandleClientAsync(client, cancellationToken); }
                    finally { _connectionGate.Release(); }
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting proxy TCP client connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientStream = client.GetStream();
        clientStream.ReadTimeout = 10000;
        clientStream.WriteTimeout = 10000;

        try
        {
            byte[] buffer = new byte[8192];
            int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead <= 0) return;

            string requestText = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            string[] requestLines = requestText.Split("\r\n");

            if (requestLines.Length == 0 || string.IsNullOrWhiteSpace(requestLines[0]))
            {
                return;
            }

            string[] firstLineTokens = requestLines[0].Split(' ');
            if (firstLineTokens.Length < 2) return;

            string method = firstLineTokens[0].ToUpperInvariant();
            string rawUrl = firstLineTokens[1];

            // Extract Host header
            string host = string.Empty;
            foreach (var line in requestLines)
            {
                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    host = line[5..].Trim();
                    break;
                }
            }

            string cleanDomain = CleanDomainFromHost(host);
            string fullUrl = rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? rawUrl
                : $"http://{host}{rawUrl}";

            // Evaluate URL against Rule Engine
            var evalReq = new EvaluationRequest(
                Url: fullUrl,
                Domain: cleanDomain,
                Path: rawUrl,
                Query: null,
                Title: null,
                Referrer: null,
                Browser: "ProxyEngine",
                Component: "Proxy",
                Timestamp: _timeProvider.UtcNow
            );

            var evalResult = await _ruleEngine.EvaluateAsync(evalReq, cancellationToken);

            if (evalResult.Decision == FilterDecision.Block)
            {
                _logger.LogInformation("Proxy BLOCK: {Url} ({Reason})", fullUrl, evalResult.Reason);
                byte[] blockResponse = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 403 Forbidden\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    "Connection: close\r\n\r\n" +
                    "<html><body style='background:#0F172A;color:#F8FAFC;font-family:sans-serif;padding:40px;'>" +
                    "<h1>403 Forbidden — Aegis Protection</h1>" +
                    $"<p>Access to <strong>{WebUtility.HtmlEncode(fullUrl)}</strong> was intercepted by your Aegis policy.</p>" +
                    $"<p>Reason: {WebUtility.HtmlEncode(evalResult.Reason)}</p>" +
                    "</body></html>"
                );

                await clientStream.WriteAsync(blockResponse, cancellationToken);
                return;
            }

            // HTTP CONNECT Tunneling (HTTPS proxying)
            if (method == "CONNECT")
            {
                byte[] connectResponse = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                await clientStream.WriteAsync(connectResponse, cancellationToken);

                int targetPort = 443;
                string targetHost = cleanDomain;

                using var destinationClient = new TcpClient();
                await destinationClient.ConnectAsync(targetHost, targetPort, cancellationToken);
                using var destinationStream = destinationClient.GetStream();

                var t1 = clientStream.CopyToAsync(destinationStream, cancellationToken);
                var t2 = destinationStream.CopyToAsync(clientStream, cancellationToken);
                await Task.WhenAny(t1, t2); // Prompt cleanup when either side closes
            }
            else
            {
                // Standard HTTP proxying
                string targetHost = cleanDomain;
                int targetPort = 80;

                using var destinationClient = new TcpClient();
                await destinationClient.ConnectAsync(targetHost, targetPort, cancellationToken);
                using var destinationStream = destinationClient.GetStream();

                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                var t1 = clientStream.CopyToAsync(destinationStream, cancellationToken);
                var t2 = destinationStream.CopyToAsync(clientStream, cancellationToken);
                await Task.WhenAny(t1, t2); // Prompt cleanup when either side closes
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proxy stream handling completed or terminated.");
        }
        finally
        {
            client.Close();
        }
    }

    private static string CleanDomainFromHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return string.Empty;

        string trimmed = host.Trim();
        if (trimmed.StartsWith("["))
        {
            // IPv6 host e.g. [::1]:8080
            int endBracket = trimmed.IndexOf(']');
            if (endBracket > 0)
            {
                return trimmed[1..endBracket];
            }
        }

        int colonIdx = trimmed.IndexOf(':');
        return colonIdx > 0 ? trimmed[..colonIdx] : trimmed;
    }
}
