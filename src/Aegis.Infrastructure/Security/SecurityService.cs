using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Infrastructure.Security;

internal class SecurityService : ISecurityService
{
    public Task<HandshakeResponse> AuthenticateHandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public bool ValidateSessionToken(string token, string requiredComponentId) => throw new NotImplementedException();
    public string ComputeRowHmac(string payload) => throw new NotImplementedException();
    public bool VerifyRowHmac(string payload, string expectedHmac) => throw new NotImplementedException();
    public byte[] ProtectData(byte[] data) => throw new NotImplementedException();
    public byte[] UnprotectData(byte[] protectedData) => throw new NotImplementedException();
}
