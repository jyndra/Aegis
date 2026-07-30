using System.Security.Cryptography;
using System.Text;
using Aegis.Core.Errors;
using Aegis.Core.Models;
using Aegis.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class SecurityServiceHandshakeTests
{
    private readonly SecurityService _securityService;
    private const string DevSecret = "aegis-extension-secret-dev";

    public SecurityServiceHandshakeTests()
    {
        _securityService = new SecurityService(NullLogger<SecurityService>.Instance);
    }

    [Fact]
    public async Task AuthenticateHandshakeAsync_ValidHmacSignature_IssuesJwtToken()
    {
        string componentId = "aegis-extension-chrome";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string payload = $"{componentId}:{timestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DevSecret));
        string signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        var req = new HandshakeRequest(componentId, timestamp, signature);

        var resp = await _securityService.AuthenticateHandshakeAsync(req);

        resp.Should().NotBeNull();
        resp.Token.Should().NotBeNullOrEmpty();
        resp.ExpiresInSeconds.Should().Be(300);

        bool isValidToken = _securityService.ValidateSessionToken(resp.Token, componentId);
        isValidToken.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateHandshakeAsync_InvalidSignature_ThrowsAegisException()
    {
        string componentId = "aegis-extension-chrome";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var req = new HandshakeRequest(componentId, timestamp, "INVALID_HMAC_SIGNATURE");

        Func<Task> act = async () => await _securityService.AuthenticateHandshakeAsync(req);

        await act.Should().ThrowAsync<AegisException>()
            .Where(e => e.ErrorCode == AegisErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task AuthenticateHandshakeAsync_ExpiredTimestamp_ThrowsAegisException()
    {
        string componentId = "aegis-extension-chrome";
        long expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

        string payload = $"{componentId}:{expiredTimestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DevSecret));
        string signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        var req = new HandshakeRequest(componentId, expiredTimestamp, signature);

        Func<Task> act = async () => await _securityService.AuthenticateHandshakeAsync(req);

        await act.Should().ThrowAsync<AegisException>()
            .Where(e => e.ErrorCode == AegisErrorCodes.TokenExpired);
    }
}
