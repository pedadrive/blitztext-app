using Blitztext.Core.Services;
using Xunit;

namespace Blitztext.Core.Tests;

public class TranscriptionQualityServiceTests
{
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.29, true)]
    [InlineData(0.3, false)]
    [InlineData(1.0, false)]
    public void ShouldRejectRecording_UsesMinimumDuration(double duration, bool expected)
    {
        Assert.Equal(expected, TranscriptionQualityService.ShouldRejectRecording(duration));
    }

    [Fact]
    public void CleanedTranscript_TrimsWhitespace()
    {
        Assert.Equal("hallo welt", TranscriptionQualityService.CleanedTranscript("  hallo welt \n"));
    }

    [Fact]
    public void IsLikelyArtifact_EmptyText_IsArtifact()
    {
        Assert.True(TranscriptionQualityService.IsLikelyArtifact("   ", 1.0));
    }

    [Fact]
    public void IsLikelyArtifact_NoLetters_IsArtifact()
    {
        Assert.True(TranscriptionQualityService.IsLikelyArtifact("12345 !!!", 2.0));
    }

    [Fact]
    public void IsLikelyArtifact_ShortRecordingWithManyWords_IsArtifact()
    {
        // < 0.55s but 5+ words -> hallucination
        Assert.True(TranscriptionQualityService.IsLikelyArtifact("eins zwei drei vier fünf", 0.4));
    }

    [Fact]
    public void IsLikelyArtifact_ShortRecordingWithLongText_IsArtifact()
    {
        var longText = new string('a', 60); // >= 56 chars, < 0.8s
        Assert.True(TranscriptionQualityService.IsLikelyArtifact(longText, 0.7));
    }

    [Fact]
    public void IsLikelyArtifact_NormalRecording_IsNotArtifact()
    {
        Assert.False(TranscriptionQualityService.IsLikelyArtifact("Das ist ein normaler Satz.", 2.5));
    }

    [Fact]
    public void IsLikelyArtifact_ShortButFewWords_IsNotArtifact()
    {
        Assert.False(TranscriptionQualityService.IsLikelyArtifact("ja gut", 0.4));
    }
}
