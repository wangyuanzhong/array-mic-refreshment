using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class StubWakeWordDetectorTests
{
    [Fact]
    public void SimulateDetection_RaisesEvent_WhenRunning()
    {
        using var detector = new StubWakeWordDetector("hello");
        WakeWordDetectedEventArgs? args = null;
        detector.WakeWordDetected += (_, e) => args = e;
        detector.Start();
        detector.SimulateDetection();

        Assert.NotNull(args);
        Assert.Equal("hello", args!.Keyword);
    }

    [Fact]
    public void SimulateDetection_NoOp_WhenStopped()
    {
        using var detector = new StubWakeWordDetector();
        var count = 0;
        detector.WakeWordDetected += (_, _) => count++;
        detector.SimulateDetection();
        Assert.Equal(0, count);
    }
}
