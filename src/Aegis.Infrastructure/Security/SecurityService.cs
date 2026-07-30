using System.Security.Cryptography;
using System.Text;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Security;

public class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private readonly byte[] _hmacKey;

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        // Default dev key; in production DPAPI-protected key file is loaded
        _hmacKey = Encoding.UTF8.GetBytes("AegisLocalSecurityDefaultSecretKey32Bytes!");
    }

    public string ComputeRowHmac(string payload)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash);
    }

    public bool VerifyRowHmac(string payload, string expectedHmac)
    {
        if (string.IsNullOrWhiteSpace(expectedHmac))
            return false;

        string computed = ComputeRowHmac(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(expectedHmac.ToUpperInvariant())
        );
    }

    public byte[] ProtectData(byte[] data)
    {
        if (data == null || data.Length == 0)
            return Array.Empty<byte>();

        try
        {
            return ProtectedData.Protect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DPAPI ProtectData failed");
            throw;
        }
    }

    public byte[] UnprotectData(byte[] protectedData)
    {
        if (protectedData == null || protectedData.Length == 0)
            return Array.Empty<byte>();

        try
        {
            return ProtectedData.Unprotect(protectedData, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DPAPI UnprotectData failed");
            throw;
        }
    }

    public Task<HandshakeResponse> AuthenticateHandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool ValidateSessionToken(string token, string requiredComponentId)
    {
        throw new NotImplementedException();
    }
}
