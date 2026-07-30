using Aegis.Core.Interfaces;

namespace Aegis.Infrastructure.Configuration;

internal class ConfigValidator : IConfigValidator
{
    public bool ValidateServiceConfig(out List<string> errors)
    {
        errors = new List<string>();
        return true;
    }
}
