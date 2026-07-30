namespace Aegis.Core.Interfaces;

public interface IConfigValidator
{
    bool ValidateServiceConfig(out List<string> errors);
}
