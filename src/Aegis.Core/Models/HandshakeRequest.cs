namespace Aegis.Core.Models;

/// <summary>
/// Handshake request payload for extension and UI clients.
/// </summary>
public record HandshakeRequest(
    string ComponentId,
    long Timestamp,
    string HmacSignature
);
