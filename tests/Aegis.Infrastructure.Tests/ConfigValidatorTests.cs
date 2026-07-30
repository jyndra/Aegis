using Aegis.Infrastructure.Configuration;
using FluentAssertions;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class ConfigValidatorTests
{
    [Fact]
    public void ValidateServiceConfig_ReturnsTrue_WithNoErrors()
    {
        var validator = new ConfigValidator();

        bool isValid = validator.ValidateServiceConfig(out var errors);

        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }
}
