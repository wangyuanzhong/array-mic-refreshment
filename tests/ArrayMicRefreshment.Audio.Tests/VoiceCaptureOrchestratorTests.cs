using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio.Tests;

public sealed class VoiceCaptureOrchestratorTests
{
    [Fact]
    public async Task PttOnly_ForwardsPtt_IgnoresWake()
    {
        using var harness = CreateHarness(VoiceTriggerMode.PttOnly);
        var pttCount = 0;
        var wakeCount = 0;
        harness.Orchestrator.UtteranceReady += (_, e) =>
        {
            if (e.TriggerKind == VoiceTriggerKind.Ptt)
            {
                pttCount++;
            }
            else
            {
                wakeCount++;
            }
        };

        await harness.RaisePttUtteranceAsync();
        harness.RaiseWakeUtterance();

        Assert.Equal(1, pttCount);
        Assert.Equal(0, wakeCount);
        Assert.False(harness.FakeWake.IsListening);
    }

    [Fact]
    public async Task WakeWordOnly_ForwardsWake_IgnoresPtt()
    {
        using var harness = CreateHarness(VoiceTriggerMode.WakeWordOnly);
        var pttCount = 0;
        var wakeCount = 0;
        harness.Orchestrator.UtteranceReady += (_, e) =>
        {
            if (e.TriggerKind == VoiceTriggerKind.Ptt)
            {
                pttCount++;
            }
            else
            {
                wakeCount++;
            }
        };

        await harness.RaisePttUtteranceAsync();
        harness.RaiseWakeUtterance();

        Assert.Equal(0, pttCount);
        Assert.Equal(1, wakeCount);
        Assert.True(harness.FakeWake.IsListening);
    }

    [Fact]
    public void SetMode_FromPttOnly_ToWakeWordOnly_StartsWakeListening()
    {
        using var harness = CreateHarness(VoiceTriggerMode.PttOnly);
        Assert.False(harness.FakeWake.IsListening);

        harness.Orchestrator.SetMode(VoiceTriggerMode.WakeWordOnly);

        Assert.Equal(VoiceTriggerMode.WakeWordOnly, harness.Orchestrator.Mode);
        Assert.True(harness.FakeWake.IsListening);
    }

    [Fact]
    public async Task SetMode_BackToPttOnly_StopsWakeListening_PttStillWorks()
    {
        using var harness = CreateHarness(VoiceTriggerMode.WakeWordOnly);
        Assert.True(harness.FakeWake.IsListening);

        harness.Orchestrator.SetMode(VoiceTriggerMode.PttOnly);
        Assert.False(harness.FakeWake.IsListening);

        UtteranceCaptureEventArgs? captured = null;
        harness.Orchestrator.UtteranceReady += (_, e) => captured = e;
        await harness.RaisePttUtteranceAsync();

        Assert.NotNull(captured);
        Assert.Equal(VoiceTriggerKind.Ptt, captured!.TriggerKind);
    }

    [Fact]
    public void Both_WhenPttHeld_DropsWakeUtterance()
    {
        using var harness = CreateHarness(VoiceTriggerMode.Both);
        harness.Ptt.RaisePressed();

        var wakeCount = 0;
        harness.Orchestrator.UtteranceReady += (_, e) =>
        {
            if (e.TriggerKind == VoiceTriggerKind.WakeWord)
            {
                wakeCount++;
            }
        };

        harness.RaiseWakeUtterance();
        harness.Ptt.RaiseReleased();

        Assert.Equal(0, wakeCount);
        Assert.True(harness.FakeWake.IsListening);
    }

    [Fact]
    public async Task WakeWord_EndToEnd_StubDetector_EmitsWakeUtterance()
    {
        const int sampleRate = 16000;
        var sine = PcmResampler.GenerateSineWavePcm16(sampleRate, 1, 440, TimeSpan.FromMilliseconds(2000));
        var silence = new byte[sine.Length / 2];
        var payload = new byte[sine.Length + silence.Length];
        sine.CopyTo(payload, 0);
        var device = new AudioDeviceInfo
        {
            Id = "fake:0",
            DisplayName = "Fake",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = sampleRate,
            Channels = 1,
        };
        var enumerator = new FakeAudioDeviceEnumerator(device);
        var factory = new FakeCaptureStreamFactory(_ => new FakeCaptureStream(payload, sampleRate, 1));
        var detector = new StubWakeWordDetector(chunksBeforeAutoFire: 0);
        var settings = new AppSettings();

        using var wake = new WakeWordCaptureService(
            settings,
            detector,
            enumerator,
            factory,
            postWakeSilenceTimeout: TimeSpan.FromMilliseconds(80),
            postWakeStartGrace: TimeSpan.FromSeconds(2),
            postWakeMaxSession: TimeSpan.FromSeconds(5),
            postWakeEchoIgnore: TimeSpan.Zero);
        var ptt = new TestPushToTalkSource();
        using var pttCapture = new PttCaptureService(settings, ptt, enumerator, factory);
        using var orchestrator = new VoiceCaptureOrchestrator(
            pttCapture,
            wake,
            VoiceTriggerMode.WakeWordOnly);

        UtteranceCaptureEventArgs? captured = null;
        orchestrator.UtteranceReady += (_, e) => captured = e;

        wake.StartListening();
        await Task.Delay(100);
        detector.SimulateDetection();
        await Task.Delay(1200);

        Assert.NotNull(captured);
        Assert.Equal(VoiceTriggerKind.WakeWord, captured!.TriggerKind);
        Assert.True(captured.Utterance.Pcm16LeMono.Length > 0);
    }

    private static Harness CreateHarness(VoiceTriggerMode mode)
    {
        var settings = new AppSettings();
        var ptt = new TestPushToTalkSource();
        var device = new AudioDeviceInfo
        {
            Id = "fake:0",
            DisplayName = "Fake",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = 16000,
            Channels = 1,
        };
        var payload = PcmResampler.GenerateSineWavePcm16(
            16000,
            1,
            220,
            TimeSpan.FromMilliseconds(300));
        var enumerator = new FakeAudioDeviceEnumerator(device);
        var factory = new FakeCaptureStreamFactory(_ => new FakeCaptureStream(payload, 16000, 1));
        var pttCapture = new PttCaptureService(settings, ptt, enumerator, factory);
        var fakeWake = new FakeWakeWordCaptureService();
        var orchestrator = new VoiceCaptureOrchestrator(pttCapture, fakeWake, mode);
        return new Harness(orchestrator, pttCapture, fakeWake, ptt);
    }

    private sealed class Harness : IDisposable
    {
        public Harness(
            VoiceCaptureOrchestrator orchestrator,
            PttCaptureService pttCapture,
            FakeWakeWordCaptureService fakeWake,
            TestPushToTalkSource ptt)
        {
            Orchestrator = orchestrator;
            PttCapture = pttCapture;
            FakeWake = fakeWake;
            Ptt = ptt;
        }

        public VoiceCaptureOrchestrator Orchestrator { get; }
        public PttCaptureService PttCapture { get; }
        public FakeWakeWordCaptureService FakeWake { get; }
        public TestPushToTalkSource Ptt { get; }

        public async Task RaisePttUtteranceAsync()
        {
            Ptt.RaisePressed();
            await Task.Delay(80);
            Ptt.RaiseReleased();
            await Task.Delay(80);
        }

        public void RaiseWakeUtterance()
        {
            FakeWake.RaiseUtterance();
        }

        public void Dispose()
        {
            Orchestrator.Dispose();
            PttCapture.Dispose();
        }
    }

    private sealed class FakeWakeWordCaptureService : IWakeWordCaptureService
    {
        private bool _listening;

        public event EventHandler<UtteranceCaptureEventArgs>? UtteranceReady;
        public event EventHandler<Exception>? CaptureFailed;
        public event EventHandler<string>? CaptureEmpty;
        public event EventHandler<string>? StatusChanged;
        public event EventHandler<WakeWordDetectedEventArgs>? WakeWordActivated;

        public bool IsListening => _listening;
        public bool IsDictationActive { get; private set; }

        public void StartListening()
        {
            _listening = true;
            StatusChanged?.Invoke(this, "fake-listening");
        }

        public void StopListening() => _listening = false;

        public void ReleaseMicForPtt() => _listening = false;

        public void RaiseUtterance()
        {
            var utterance = new AudioUtterance
            {
                Pcm16LeMono = new byte[6400],
                SampleRate = 16000,
                Duration = TimeSpan.FromMilliseconds(200),
            };
            UtteranceReady?.Invoke(
                this,
                new UtteranceCaptureEventArgs(utterance, VoiceTriggerKind.WakeWord));
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestPushToTalkSource : IPushToTalkSource
    {
        public event EventHandler? PttPressed;
        public event EventHandler? PttReleased;
        public string HotkeyDisplay { get; } = "Test";

        public void RaisePressed() => PttPressed?.Invoke(this, EventArgs.Empty);

        public void RaiseReleased() => PttReleased?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TriggeringVoiceActivityDetector : IVoiceActivityDetector
    {
        public bool TriggerEndOfSpeech { get; set; }

        public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate) =>
            TriggerEndOfSpeech;

        public void Reset() => TriggerEndOfSpeech = false;
    }
}
