using Xunit;
using PickleIQ.Infrastructure.Services;

namespace PickleIQ.Tests.Services;

public class RallyDetectionServiceTests
{
    [Theory]
    [InlineData(1920, 1080, 640, 360)]  // 16:9 → 360 (already even)
    [InlineData(1280, 720, 640, 360)]   // 16:9 smaller
    [InlineData(3840, 2160, 640, 360)]  // 4K 16:9
    [InlineData(1920, 1440, 640, 480)]  // 4:3
    [InlineData(1920, 1280, 640, 428)]  // 3:2 → 426.67 rounds to 427 → +1 = 428
    public void ComputeScaledHeight_ReturnsNearestEvenHeight(int w, int h, int targetW, int expected)
    {
        var result = RallyDetectionService.ComputeScaledHeight(w, h, targetW);
        Assert.Equal(expected, result);
        Assert.Equal(0, result % 2);
    }
}
