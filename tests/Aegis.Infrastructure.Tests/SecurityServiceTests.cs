using System.Text;
using Aegis.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class SecurityServiceTests
{
    private readonly SecurityService _securityService;

    public SecurityServiceTests()
    {
        _securityService = new SecurityService(NullLogger<SecurityService>.Instance);
    }

    [Fact]
    public void ComputeRowHmac_ProducesConsistentHexHash()
    {
        string payload = "lock_state:id=1;is_locked=1;expires_at=2026-08-25T00:00:00Z";

        string hash1 = _securityService.ComputeRowHmac(payload);
        string hash2 = _securityService.ComputeRowHmac(payload);

        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void VerifyRowHmac_ReturnsTrue_ForValidHmac()
    {
        string payload = "test-payload-data";
        string hmac = _securityService.ComputeRowHmac(payload);

        bool isValid = _securityService.VerifyRowHmac(payload, hmac);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyRowHmac_ReturnsFalse_ForModifiedPayload()
    {
        string payload = "original-payload";
        string hmac = _securityService.ComputeRowHmac(payload);

        bool isValid = _securityService.VerifyRowHmac("tampered-payload", hmac);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ProtectAndUnprotect_RoundtripsOriginalData()
    {
        byte[] original = Encoding.UTF8.GetBytes("SuperSecretData123");

        byte[] protectedBytes = _securityService.ProtectData(original);
        byte[] unprotectedBytes = _securityService.UnprotectData(protectedBytes);

        protectedBytes.Should().NotEqual(original);
        unprotectedBytes.Should().Equal(original);
    }
}
