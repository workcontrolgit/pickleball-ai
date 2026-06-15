using PickleIQ.Infrastructure.Services;
using Xunit;

namespace PickleIQ.Tests.Services;

public class RallyDetectionServiceTests
{
    // --- ComputeScaledHeight (existing) ---

    [Theory]
    [InlineData(1920, 1080, 640, 360)]
    [InlineData(1280, 720,  640, 360)]
    [InlineData(3840, 2160, 640, 360)]
    [InlineData(1080, 1920, 640, 1138)]
    [InlineData(1920, 1081, 640, 360)] // odd → rounded up to even
    public void ComputeScaledHeight_ReturnsEvenHeight(int w, int h, int targetW, int expectedH)
    {
        var result = RallyDetectionService.ComputeScaledHeight(w, h, targetW);
        Assert.Equal(expectedH, result);
        Assert.Equal(0, result % 2);
    }

    // --- IsFrameActive ---

    [Fact]
    public void IsFrameActive_BothConditionsMet_ReturnsTrue()
    {
        Assert.True(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: true));
    }

    [Fact]
    public void IsFrameActive_NoBall_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 4, minPlayers: 2, ballDetected: false));
    }

    [Fact]
    public void IsFrameActive_TooFewPlayers_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 2, ballDetected: true));
    }

    [Fact]
    public void IsFrameActive_NoPlayersNoBall_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 0, minPlayers: 2, ballDetected: false));
    }

    [Theory]
    [InlineData(true,  true)]   // ball detected → active
    [InlineData(false, false)]  // no ball → inactive
    public void IsFrameActive_SinglePlayerMode_BallRequired(bool ballDetected, bool expected)
    {
        // FollowCam/Training use minPlayers=1
        var result = RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 1, ballDetected: ballDetected);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsFrameActive_PersonsAtThresholdButNoBall_ReturnsFalse()
    {
        // Persons exactly meet threshold but no ball — AND gate must reject
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: false));
    }

    // --- IsFrameActive with pose ---

    [Fact]
    public void IsFrameActive_WithPose_AllConditionsMet_ReturnsTrue()
    {
        Assert.True(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: true, anyPlayerSwinging: true));
    }

    [Fact]
    public void IsFrameActive_WithPose_NoSwing_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: true, anyPlayerSwinging: false));
    }

    [Fact]
    public void IsFrameActive_WithPose_NoBall_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 2, minPlayers: 2, ballDetected: false, anyPlayerSwinging: true));
    }

    // --- AnyPlayerSwinging ---

    [Fact]
    public void AnyPlayerSwinging_WristAboveShoulder_ReturnsTrue()
    {
        // Image coords: Y increases downward. Wrist above shoulder = smaller Y.
        var keypoints = new (float X, float Y, float Confidence)[17];
        keypoints[5] = (100f, 200f, 0.9f);  // left shoulder
        keypoints[6] = (200f, 210f, 0.9f);  // right shoulder
        keypoints[9] = (100f, 150f, 0.9f);  // left wrist — above shoulder
        keypoints[10] = (200f, 230f, 0.9f); // right wrist — below shoulder

        Assert.True(RallyDetectionService.AnyPlayerSwinging(
            new[] { keypoints }));
    }

    [Fact]
    public void AnyPlayerSwinging_WristsBelow_ReturnsFalse()
    {
        var keypoints = new (float X, float Y, float Confidence)[17];
        keypoints[5] = (100f, 200f, 0.9f);  // left shoulder
        keypoints[6] = (200f, 210f, 0.9f);  // right shoulder
        keypoints[9] = (100f, 280f, 0.9f);  // left wrist — below shoulder
        keypoints[10] = (200f, 290f, 0.9f); // right wrist — below shoulder

        Assert.False(RallyDetectionService.AnyPlayerSwinging(
            new[] { keypoints }));
    }

    [Fact]
    public void AnyPlayerSwinging_EmptyList_ReturnsFalse()
    {
        Assert.False(RallyDetectionService.AnyPlayerSwinging(
            Array.Empty<(float X, float Y, float Confidence)[]>()));
    }

    [Fact]
    public void AnyPlayerSwinging_LowConfidenceKeypoints_ReturnsFalse()
    {
        var keypoints = new (float X, float Y, float Confidence)[17];
        keypoints[5] = (100f, 200f, 0.1f);  // left shoulder — low confidence
        keypoints[9] = (100f, 150f, 0.1f);  // left wrist — low confidence

        Assert.False(RallyDetectionService.AnyPlayerSwinging(
            new[] { keypoints }));
    }
}
