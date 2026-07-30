using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IAiImageClassifier
{
    Task<ImageClassificationResult> ClassifyImageBytesAsync(byte[]? imageBytes, CancellationToken cancellationToken = default);
}
