using System.Text.Json.Serialization;

namespace Aegis.Core.Models;

/// <summary>
/// Event or decision severity rating.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterSeverity
{
    Info,
    Warning,
    Critical,
    Tamper
}
