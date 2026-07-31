using System.Text.Json.Serialization;

namespace Aegis.Core.Models;

/// <summary>
/// Decision outcome from rule engine evaluation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterDecision
{
    Allow,
    Block,
    Redirect,
    Degraded
}
