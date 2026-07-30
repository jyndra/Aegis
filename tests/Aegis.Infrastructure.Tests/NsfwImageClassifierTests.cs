using Aegis.Core.Interfaces;
using Aegis.Infrastructure.Ai;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aegis.Infrastructure.Tests;

public class NsfwImageClassifierTests
{
    private readonly Mock<IOnnxInferenceEngine> _mockOnnx = new();
    private readonly NsfwImageClassifier _classifier;

    public NsfwImageClassifierTests()
    {
        _classifier = new NsfwImageClassifier(_mockOnnx.Object);
    }

    [Fact]
    public async Task Gate1_SmallIconOrAvatar_InstantlyApprovedWithoutProcessing()
    {
        // Image byte array smaller than 640 bytes (e.g., 300 bytes)
        byte[] tinyIcon = new byte[300];
        
        var result = await _classifier.ClassifyImageBytesAsync(tinyIcon);

        result.IsExplicit.Should().BeFalse();
        result.Category.Should().Be("Safe (Gate 1: Tiny Icon)");
        _mockOnnx.Verify(o => o.EvaluateNsfwProbabilityAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Gate2_LowSkinToneImage_ApprovedWithoutOnnxInvocation()
    {
        // Large pure blue image (> 640 bytes) with 0% skin tone in YCbCr
        byte[] blueImage = new byte[1500];
        for (int i = 0; i < blueImage.Length; i += 3)
        {
            blueImage[i] = 0;     // R
            blueImage[i + 1] = 0; // G
            blueImage[i + 2] = 255; // B
        }

        var result = await _classifier.ClassifyImageBytesAsync(blueImage);

        result.SkinTonePercentage.Should().Be(0.0);
        result.IsExplicit.Should().BeFalse();
        result.Category.Should().Be("Safe (Gate 2: Low Skin Tone)");
        
        // Ensure AI neural net was completely bypassed!
        _mockOnnx.Verify(o => o.EvaluateNsfwProbabilityAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Gate3_HighSkinToneWithExplicitOnnxScore_DetectsExplicitAdult()
    {
        // Synthetic skin-tone RGB pixels (>640 bytes) => Y=174, Cb=109, Cr=161 (in skin limits)
        byte[] skinImage = new byte[3000];
        for (int i = 0; i < skinImage.Length; i += 3)
        {
            skinImage[i] = 210;   // R
            skinImage[i + 1] = 160; // G
            skinImage[i + 2] = 140; // B
        }

        // Mock ONNX Neural Net returning explicit high probability (0.85)
        _mockOnnx.Setup(o => o.EvaluateNsfwProbabilityAsync(skinImage, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(0.85);

        var result = await _classifier.ClassifyImageBytesAsync(skinImage);

        result.SkinTonePercentage.Should().BeGreaterThanOrEqualTo(90.0);
        result.NsfwProbability.Should().Be(0.85);
        result.IsExplicit.Should().BeTrue();
        result.Category.Should().Be("ExplicitAdult (Gate 3: ONNX Positive)");
        
        _mockOnnx.Verify(o => o.EvaluateNsfwProbabilityAsync(skinImage, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gate3_HighSkinToneWithModerateOnnxScore_ClassifiesAsSuggestiveSwimsuit()
    {
        byte[] skinImage = new byte[3000];
        for (int i = 0; i < skinImage.Length; i += 3)
        {
            skinImage[i] = 210;   
            skinImage[i + 1] = 160; 
            skinImage[i + 2] = 140; 
        }

        // Mock ONNX Neural Net recognizing benign portrait / swimwear (0.50 probability)
        _mockOnnx.Setup(o => o.EvaluateNsfwProbabilityAsync(skinImage, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(0.50);

        var result = await _classifier.ClassifyImageBytesAsync(skinImage);

        result.IsExplicit.Should().BeFalse();
        result.Category.Should().Be("Suggestive (Gate 3: Portrait / Swimwear)");
    }
}
