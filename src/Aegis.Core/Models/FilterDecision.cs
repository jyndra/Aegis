namespace Aegis.Core.Models;

/// <summary>
/// Decision outcome from rule engine evaluation.
/// </summary>
public enum FilterDecision
{
    Allow,
    Block,
    Redirect,
    Degraded
}
