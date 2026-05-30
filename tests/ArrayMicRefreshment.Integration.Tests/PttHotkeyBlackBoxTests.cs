#if WINDOWS

using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;

namespace ArrayMicRefreshment.Integration.Tests;

/// <summary>
/// Black-box PTT: real WH_KEYBOARD_LL hook + synthetic SendInput (no manual hotkey testing).
/// </summary>
[Collection("PttBlackBox")]
public sealed class PttHotkeyBlackBoxTests
{
    private const ushort TestVk = 0x87; // F24 — unlikely to collide with user shortcuts
    private const string TestHotkey = "Ctrl+Shift+F24";

    [Fact]
    [Trait("Category", "PttBlackBox")]
    public void NAudioPushToTalkSource_registers_default_hotkey()
    {
        using var ptt = new NAudioPushToTalkSource("Ctrl+Alt+Space");
        Assert.True(ptt.IsRegistered);
        Assert.Equal("Ctrl+Alt+Space", ptt.HotkeyDisplay);
    }

    [Fact]
    [Trait("Category", "PttBlackBox")]
    public async Task Low_level_hook_detects_press_and_release_via_sendinput()
    {
        if (!WindowsKeyboardSimulator.CanSimulateInput())
        {
            return; // headless / non-interactive session — registration test still runs
        }

        var pressed = new ManualResetEventSlim(false);
        var released = new ManualResetEventSlim(false);
        Exception? fault = null;
        var ready = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                ApplicationConfiguration.Initialize();
                using var pump = new Form
                {
                    ShowInTaskbar = false,
                    Opacity = 0,
                    Size = new Size(1, 1),
                    Location = new Point(-32000, -32000),
                };

                using var ptt = new NAudioPushToTalkSource(TestHotkey);
                if (!ptt.IsRegistered)
                {
                    throw new InvalidOperationException("PTT hook failed to register for " + TestHotkey);
                }

                ptt.PttPressed += (_, _) => pressed.Set();
                ptt.PttReleased += (_, _) => released.Set();

                pump.Show();
                ready.Set();

                var exitTimer = new System.Windows.Forms.Timer { Interval = 8000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    pump.Close();
                };
                exitTimer.Start();
                Application.Run(pump);
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            finally
            {
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "PttBlackBoxHook",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)), "STA message pump did not become ready");
        await Task.Delay(150);

        WindowsKeyboardSimulator.TapChord(ctrl: true, shift: true, alt: false, win: false, vk: TestVk, holdMs: 150);

        Assert.True(pressed.Wait(TimeSpan.FromSeconds(3)), "PTT press was not detected by keyboard hook");
        Assert.True(released.Wait(TimeSpan.FromSeconds(3)), "PTT release was not detected by keyboard hook");
        Assert.Null(fault);
        Assert.True(finished.Wait(TimeSpan.FromSeconds(10)), "STA message pump did not exit cleanly");
    }

    [Fact]
    [Trait("Category", "PttBlackBox")]
    public async Task PttCaptureService_with_real_hook_emits_utterance_on_release()
    {
        if (!WindowsKeyboardSimulator.CanSimulateInput())
        {
            return;
        }

        const int sampleRate = 16000;
        const int channels = 1;
        var duration = TimeSpan.FromMilliseconds(300);
        var payload = PcmResampler.GenerateSineWavePcm16(sampleRate, channels, 440, duration);
        var device = new AudioDeviceInfo
        {
            Id = "fake:ptt-blackbox",
            DisplayName = "Fake Mic",
            HostApi = AudioHostApi.Wasapi,
            IsDefault = true,
            DefaultSampleRate = sampleRate,
            Channels = channels,
        };

        AudioUtterance? captured = null;
        var utteranceReady = new ManualResetEventSlim(false);
        Exception? fault = null;
        var ready = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                ApplicationConfiguration.Initialize();
                using var pump = new Form
                {
                    ShowInTaskbar = false,
                    Opacity = 0,
                    Size = new Size(1, 1),
                    Location = new Point(-32000, -32000),
                };

                using var ptt = new NAudioPushToTalkSource(TestHotkey);
                if (!ptt.IsRegistered)
                {
                    throw new InvalidOperationException("PTT hook failed to register for capture test");
                }

                var enumerator = new FakeAudioDeviceEnumerator(device);
                var factory = new FakeCaptureStreamFactory(_ => new FakeCaptureStream(payload, sampleRate, channels));
                var vad = new ImmediateEndVoiceActivityDetector();
                var settings = new AppSettings();

                using var capture = new PttCaptureService(
                    settings,
                    ptt,
                    enumerator,
                    factory,
                    vad,
                    pttCaptureAllowed: () => true,
                    keepStandbyCaptureBetweenSessions: () => false,
                    vadAssistMinHold: TimeSpan.Zero);

                capture.UtteranceReady += (_, u) =>
                {
                    captured = u;
                    utteranceReady.Set();
                };

                pump.Show();
                ready.Set();

                var exitTimer = new System.Windows.Forms.Timer { Interval = 10000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    pump.Close();
                };
                exitTimer.Start();
                Application.Run(pump);
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            finally
            {
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "PttBlackBoxCapture",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)), "Capture STA pump did not become ready");
        await Task.Delay(150);

        WindowsKeyboardSimulator.TapChord(ctrl: true, shift: true, alt: false, win: false, vk: TestVk, holdMs: 200);

        Assert.True(utteranceReady.Wait(TimeSpan.FromSeconds(5)), "PTT capture did not emit utterance after synthetic hotkey");
        Assert.Null(fault);
        Assert.NotNull(captured);
        Assert.True(captured!.Pcm16LeMono.Length > 0);
        Assert.True(finished.Wait(TimeSpan.FromSeconds(12)), "Capture STA pump did not exit cleanly");
    }

    [Fact]
    [Trait("Category", "PttBlackBox")]
    public async Task Default_hotkey_CtrlAltSpace_detects_press_and_release()
    {
        if (!WindowsKeyboardSimulator.CanSimulateInput())
        {
            return;
        }

        const ushort spaceVk = 0x20;
        var pressed = new ManualResetEventSlim(false);
        var released = new ManualResetEventSlim(false);
        Exception? fault = null;
        var ready = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                ApplicationConfiguration.Initialize();
                using var pump = new Form
                {
                    ShowInTaskbar = false,
                    Opacity = 0,
                    Size = new Size(1, 1),
                    Location = new Point(-32000, -32000),
                };

                using var ptt = new NAudioPushToTalkSource("Ctrl+Alt+Space");
                if (!ptt.IsRegistered)
                {
                    throw new InvalidOperationException("PTT hook failed for Ctrl+Alt+Space");
                }

                ptt.PttPressed += (_, _) => pressed.Set();
                ptt.PttReleased += (_, _) => released.Set();

                pump.Show();
                ready.Set();

                var exitTimer = new System.Windows.Forms.Timer { Interval = 8000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    pump.Close();
                };
                exitTimer.Start();
                Application.Run(pump);
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            finally
            {
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "PttBlackBoxCtrlAltSpace",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(150);

        WindowsKeyboardSimulator.TapChord(ctrl: true, shift: false, alt: true, win: false, vk: spaceVk, holdMs: 150);

        Assert.True(pressed.Wait(TimeSpan.FromSeconds(3)), "Ctrl+Alt+Space press was not detected");
        Assert.True(released.Wait(TimeSpan.FromSeconds(3)), "Ctrl+Alt+Space release was not detected");
        Assert.Null(fault);
        Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    [Trait("Category", "PttBlackBox")]
    public async Task ApplicationContext_pump_detects_CtrlAltSpace_like_tray_app()
    {
        if (!WindowsKeyboardSimulator.CanSimulateInput())
        {
            return;
        }

        const ushort spaceVk = 0x20;
        var pressed = new ManualResetEventSlim(false);
        var ready = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                ApplicationConfiguration.Initialize();
                var anchor = new Form
                {
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    Size = new Size(1, 1),
                };
                anchor.Show();

                using var ptt = new NAudioPushToTalkSource("Ctrl+Alt+Space", registerImmediately: false);
                ptt.PttPressed += (_, _) => pressed.Set();

                Application.Idle += OnIdleOnce;
                void OnIdleOnce(object? s, EventArgs e)
                {
                    Application.Idle -= OnIdleOnce;
                    ptt.TryUpdateHotkey("Ctrl+Alt+Space", out _);
                    ready.Set();
                }

                var exitTimer = new System.Windows.Forms.Timer { Interval = 8000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    Application.ExitThread();
                };
                exitTimer.Start();

                Application.Run();
                anchor.Dispose();
            }
            finally
            {
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "PttBlackBoxAppContext",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(150);

        WindowsKeyboardSimulator.TapChord(ctrl: true, shift: false, alt: true, win: false, vk: spaceVk, holdMs: 150);

        Assert.True(pressed.Wait(TimeSpan.FromSeconds(3)), "Tray-style pump did not detect Ctrl+Alt+Space");
        Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
    }

    private sealed class ImmediateEndVoiceActivityDetector : IVoiceActivityDetector
    {
        private int _frames;

        public bool IsAvailable => true;

        public bool HadSpeechSinceReset => _frames > 0;

        public DateTimeOffset? LastSpeechActivityUtc =>
            _frames > 0 ? DateTimeOffset.UtcNow : null;

        public bool IsEndOfSpeech(ReadOnlySpan<short> mono16Samples, int sampleRate)
        {
            _frames++;
            return _frames >= 2;
        }

        public void ConfigureSilenceDuration(TimeSpan silence)
        {
        }

        public void Reset() => _frames = 0;
    }
}

[CollectionDefinition("PttBlackBox", DisableParallelization = true)]
public sealed class PttBlackBoxCollection
{
}

#endif
