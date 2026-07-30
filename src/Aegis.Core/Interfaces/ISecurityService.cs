using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

/// <summary>
/// Provides DPAPI cryptographic key storage, HMAC generation/validation, and JWT session tokens.
/// </summary>
public interface ISecurityService
{
    Task<HandshakeResponse> AuthenticateHandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken = default);
    bool ValidateSessionToken(string token, string requiredComponentId);
    string ComputeRowHmac(string payload);
    bool VerifyRowHmac(string payload, string expectedHmac);
    byte[] ProtectData(byte[] data);
    byte[] UnprotectData(byte[] protectedData);
}
