using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class WakeWordPttHandoffTests
{
    [Fact]
    public async Task BothModeHandoff_keepsLiveStreamWithoutReopen()
    {
        const int sampleRate = 16000;
        const int channels = 1;
        var sine = PcmResampler.GenerateSineWavePcm16(sampleRate, channels, 440, TimeSpan.FromSeconds(2));
        var device = new AudioDeviceInfo
        {
            Id = "fake:handoff",
            DisplayName = "Fake Mic",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = sampleRate,
            Channels = channels,
        };

        FakeCaptureStream? sharedStream = null;
        var factory = new FakeCaptureStreamFactory(d =>
        {
            sharedStream = new FakeCaptureStream(sine, sampleRate, channels, chunkSize: 640);
            return sharedStream;
        });
        var enumerator = new FakeAudioDeviceEnumerator(device);
        var settings = new AppSettings();
        using var wakeDetector = new StubWakeWordDetector("test");
        using var wake = new WakeWordCaptureService(
            settings,
            wakeDetector,
            enumerator,
            factory);
        var ptt = new HandoffTestPushToTalkSource();
        using var pttCapture = new PttCaptureService(
            settings,
            ptt,
            enumerator,
            factory,
            tryCaptureHandoffOnPress: wake.TryHandoffStreamForPtt);

        wake.StartListening();
        await Task.Delay(120);

        AudioUtterance? captured = null;
        pttCapture.UtteranceReady += (_, u) => captured = u;

        ptt.RaisePressed();
        await Task.Delay(200);
        ptt.RaiseReleased();
        await Task.Delay(80);

        Assert.False(wake.IsListening);
        Assert.NotNull(captured);
        Assert.True(captured!.Pcm16LeMono.Length > 6400, "handoff utterance should include pre-roll + live audio");
    }

    private sealed class HandoffTestPushToTalkSource : IPushToTalkSource
    {
        public event EventHandler? PttPressed;
        public event EventHandler? PttReleased;
        public string HotkeyDisplay { get; } = "Test";

        public void RaisePressed() => PttPressed?.Invoke(this, EventArgs.Empty);

        public void RaiseReleased() => PttReleased?.Invoke(this, EventArgs.Empty);
    }
}
