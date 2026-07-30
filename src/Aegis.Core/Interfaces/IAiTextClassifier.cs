using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IAiTextClassifier
{
    Task<TextClassificationResult> ClassifyTextAsync(string? content, CancellationToken cancellationToken = default);
}
