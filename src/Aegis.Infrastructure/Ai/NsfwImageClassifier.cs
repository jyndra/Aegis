using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.Infrastructure.Ai;

public class NsfwImageClassifier : IAiImageClassifier
{
    private readonly IOnnxInferenceEngine _onnxEngine;
    private readonly ILogger<NsfwImageClassifier> _logger;

    // YCbCr skin tone boundaries
    private const double MinY = 80.0;
    private const double MaxY = 230.0;
    private const double MinCb = 77.0;
    private const double MaxCb = 127.0;
    private const double MinCr = 133.0;
    private const double MaxCr = 173.0;

    private const int TargetSampleSize = 64 * 64; // 4,096 downsampled points
    private const int MinimumGateOneByteThreshold = 640; // ~15x15 icon byte size

    public NsfwImageClassifier(IOnnxInferenceEngine? onnxEngine = null, ILogger<NsfwImageClassifier>? logger = null)
    {
        _onnxEngine = onnxEngine ?? new OnnxInferenceEngine(NullLogger<OnnxInferenceEngine>.Instance);
        _logger = logger ?? NullLogger<NsfwImageClassifier>.Instance;
    }

    public async Task<ImageClassificationResult> ClassifyImageBytesAsync(byte[]? imageBytes, CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length < 3)
        {
            return new ImageClassificationResult(
                IsExplicit: false,
                SkinTonePercentage: 0.0,
                NsfwProbability: 0.0,
                Category: "Unrecognized"
            );
        }

        int totalBytes = imageBytes.Length;

        // ====================================================================
        // GATE 1: Dimension & Size Pre-Filter (< 0.1ms)
        // Bypasses evaluation for tiny UI icons, buttons, tracking pixels, and avatars
        // ====================================================================
        if (totalBytes < MinimumGateOneByteThreshold)
        {
            _logger.LogDebug("Image bypassed via Gate 1 (Small Icon/UI Element size: {Bytes} bytes).", totalBytes);
            return new ImageClassificationResult(false, 0.0, 0.0, "Safe (Gate 1: Tiny Icon)");
        }

        // ====================================================================
        // GATE 2: Ultra-Fast YCbCr Skin-Tone Heuristic (< 2ms)
        // Downsamples image into 64x64 grid to check human skin color presence
        // ====================================================================
        // Staff Engineer Fix: Ensure step is strictly aligned to a 3-byte RGB pixel boundary
        int step = Math.Max(1, totalBytes / (TargetSampleSize * 3)) * 3;
        int totalSamples = 0;
        int skinToneSamples = 0;

        for (int i = 0; i < totalBytes - 2; i += step)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            int r = imageBytes[i];
            int g = imageBytes[i + 1];
            int b = imageBytes[i + 2];

            double y  =  0.299 * r + 0.587 * g + 0.114 * b;
            double cb = 128.0 - 0.168736 * r - 0.331264 * g + 0.5 * b;
            double cr = 128.0 + 0.5 * r - 0.418688 * g - 0.081312 * b;

            totalSamples++;

            if (y >= MinY && y <= MaxY && cb >= MinCb && cb <= MaxCb && cr >= MinCr && cr <= MaxCr)
            {
                skinToneSamples++;
            }
        }

        if (totalSamples == 0)
        {
            return new ImageClassificationResult(false, 0.0, 0.0, "Safe");
        }

        double skinTonePercentage = Math.Round((double)skinToneSamples / totalSamples * 100.0, 2);

        // If skin tone distribution is low (< 35%), instantly approve without invoking neural net
        if (skinTonePercentage < 35.0)
        {
            _logger.LogDebug("Image approved via Gate 2 (Low skin tone: {SkinPercentage}%). AI Neural Net bypassed.", skinTonePercentage);
            return new ImageClassificationResult(false, skinTonePercentage, 0.05, "Safe (Gate 2: Low Skin Tone)");
        }

        // ====================================================================
        // GATE 3: Targeted ONNX Neural Network Inference (15-25ms CPU throttled)
        // Evaluates high-risk images to differentiate explicit anatomy from benign portraits/swimwear
        // ====================================================================
        _logger.LogInformation("Gate 3 triggered (Skin tone {SkinPercentage}% >= 35%). Invoking ONNX Vision model evaluation...", skinTonePercentage);
        double nsfwProbability = await _onnxEngine.EvaluateNsfwProbabilityAsync(imageBytes, cancellationToken);

        bool isExplicit = nsfwProbability >= 0.70 && skinTonePercentage >= 40.0;
        string category;
        if (isExplicit)
        {
            category = "ExplicitAdult (Gate 3: ONNX Positive)";
            _logger.LogWarning("Gate 3 ONNX detected Explicit Adult imagery (Prob: {Prob}, Skin: {Skin}%)", nsfwProbability, skinTonePercentage);
        }
        else if (nsfwProbability >= 0.40)
        {
            category = "Suggestive (Gate 3: Portrait / Swimwear)";
            _logger.LogInformation("Gate 3 rated image as Suggestive / Non-Explicit (Prob: {Prob})", nsfwProbability);
        }
        else
        {
            category = "Safe (Gate 3: ONNX Verified Safe)";
        }

        return new ImageClassificationResult(
            IsExplicit: isExplicit,
            SkinTonePercentage: skinTonePercentage,
            NsfwProbability: nsfwProbability,
            Category: category
        );
    }
}
