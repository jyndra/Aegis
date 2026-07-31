using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aegis.Core.Errors;
using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Aegis.Infrastructure.Security;

public class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private readonly byte[] _hmacKey;
    private readonly SymmetricSecurityKey _jwtKey;
    private const string PreSharedExtensionSecret = "aegis-extension-secret-dev";
    private const int MaxAllowableClockSkewSeconds = 120;

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        // Default dev keys; in production DPAPI-protected key files are loaded
        _hmacKey = Encoding.UTF8.GetBytes("AegisLocalSecurityDefaultSecretKey32Bytes!");
        _jwtKey = new SymmetricSecurityKey(_hmacKey);
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
        if (request == null || string.IsNullOrWhiteSpace(request.ComponentId))
        {
            throw new AegisException(AegisErrorCodes.ComponentIdentityInvalid, "Invalid component identity");
        }

        // Verify timestamp freshness (±120 seconds for clock skew tolerance)
        long nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long skew = Math.Abs(nowSeconds - request.Timestamp);

        if (skew > MaxAllowableClockSkewSeconds)
        {
            _logger.LogWarning("Handshake timestamp skewed by {SkewSec}s for component {Component}", skew, request.ComponentId);
            throw new AegisException(AegisErrorCodes.TokenExpired, $"Handshake timestamp outside allowable skew window ({skew}s > {MaxAllowableClockSkewSeconds}s)");
        }

        // Compute HMAC using pre-shared extension secret
        string payload = $"{request.ComponentId}:{request.Timestamp}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(PreSharedExtensionSecret));
        byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        string expectedSignature = Convert.ToHexString(computedHash).ToUpperInvariant();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(request.HmacSignature.ToUpperInvariant())))
        {
            _logger.LogWarning("Handshake signature mismatch for component {Component}", request.ComponentId);
            throw new AegisException(AegisErrorCodes.Unauthorized, "Handshake signature validation failed");
        }

        // Generate JWT Token (5-minute expiry)
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, request.ComponentId) }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        string jwtToken = tokenHandler.WriteToken(token);

        _logger.LogInformation("Authenticated handshake for component '{Component}'. Session token issued (Clock skew: {SkewSec}s).", request.ComponentId, skew);

        return Task.FromResult(new HandshakeResponse(
            Token: jwtToken,
            ExpiresInSeconds: 300,
            Nonce: Guid.NewGuid().ToString("N"),
            CurrentState: ProtectionState.Protected,
            IsLocked: true
        ));
    }

    public bool ValidateSessionToken(string token, string requiredComponentId)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _jwtKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromSeconds(10)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            string? sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return string.Equals(sub, requiredComponentId, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session token validation failed");
            return false;
        }
    }
}
