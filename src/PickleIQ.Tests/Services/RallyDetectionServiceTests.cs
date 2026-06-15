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

    [Fact]
    public void IsFrameActive_SinglePlayerMode_BallRequired()
    {
        // FollowCam/Training use minPlayers=1
        Assert.True(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 1, ballDetected: true));
        Assert.False(RallyDetectionService.IsFrameActive(
            personCount: 1, minPlayers: 1, ballDetected: false));
    }
}
