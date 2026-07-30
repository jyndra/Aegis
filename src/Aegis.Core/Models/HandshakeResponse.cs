namespace Aegis.Core.Models;

/// <summary>
/// Handshake response payload containing session token.
/// </summary>
public record HandshakeResponse(
    string Token,
    long ExpiresInSeconds,
    string Nonce,
    ProtectionState CurrentState,
    bool IsLocked
);
