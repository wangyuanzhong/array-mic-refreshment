using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class PttCapturePriorityTests
{
    [Fact]
    public async Task Hold_VadTrigger_Release_EmitsSingleUtteranceWithFullBuffer()
    {
        const int sampleRate = 16000;
        const int channels = 1;
        var duration = TimeSpan.FromMilliseconds(400);
        var payload = PcmResampler.GenerateSineWavePcm16(sampleRate, channels, 220, duration);

        var device = new AudioDeviceInfo
        {
            Id = "fake:0",
            DisplayName = "Fake Mic",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = sampleRate,
            Channels = channels,
        };
        var enumerator = new FakeAudioDeviceEnumerator(device);
        var factory = new FakeCaptureStreamFactory(_ => new FakeCaptureStream(payload, sampleRate, channels));
        var ptt = new TestPushToTalkSource();
        var vad = new TriggeringVoiceActivityDetector();
        var settings = new AppSettings();

        using var service = new PttCaptureService(
            settings,
            ptt,
            enumerator,
            factory,
            vad,
            vadAssistMinHold: TimeSpan.Zero);

        AudioUtterance? captured = null;
        var utteranceCount = 0;
        service.UtteranceReady += (_, u) =>
        {
            utteranceCount++;
            captured = u;
        };

        ptt.RaisePressed();
        await Task.Delay(80);
        vad.TriggerEndOfSpeech = true;
        await Task.Delay(120);
        ptt.RaiseReleased();
        await Task.Delay(80);

        Assert.Equal(1, ptt.ReleaseCount);
        Assert.Equal(1, service.ReleaseEventCount);
        Assert.Equal(1, utteranceCount);
        Assert.NotNull(captured);
        Assert.Equal(16000, captured!.SampleRate);
        Assert.True(captured.Pcm16LeMono.Length > 0);
        var expectedMinBytes = (int)(duration.TotalSeconds * 16000 * 2 * 0.5);
        Assert.True(captured.Pcm16LeMono.Length >= expectedMinBytes);
        Assert.True(service.ReleaseEventCount == 1);
    }

    private sealed class TestPushToTalkSource : IPushToTalkSource
    {
        public event EventHandler? PttPressed;
        public event EventHandler? PttReleased;
        public string HotkeyDisplay { get; } = "Test";
        public int ReleaseCount { get; private set; }

        public void RaisePressed() => PttPressed?.Invoke(this, EventArgs.Empty);

        public void RaiseReleased()
        {
            ReleaseCount++;
            PttReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TriggeringVoiceActivityDetector : IVoiceActivityDetector
    {
        public bool IsAvailable => true;

        public bool HadSpeechSinceReset { get; private set; }

        public DateTimeOffset? LastSpeechActivityUtc { get; private set; }

        public bool TriggerEndOfSpeech { get; set; }

        public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate)
        {
            HadSpeechSinceReset = true;
            LastSpeechActivityUtc = DateTimeOffset.UtcNow;
            return TriggerEndOfSpeech;
        }

        public void ConfigureSilenceDuration(TimeSpan silence)
        {
        }

        public void Reset()
        {
            TriggerEndOfSpeech = false;
            HadSpeechSinceReset = false;
            LastSpeechActivityUtc = null;
        }
    }
}
