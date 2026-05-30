#if WINDOWS

using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Integration.Tests.Support;

namespace ArrayMicRefreshment.Integration.Tests;

/// <summary>
/// E2E：启动托盘同款栈 → Web 设置保存改热键 → 模拟按新热键 → 验证开录与出段。
/// </summary>
[Collection("PttBlackBox")]
public sealed class PttHotkeyE2eTests
{
    private static string InitialHotkey =>
        Environment.GetEnvironmentVariable("AMR_PTT_E2E_INITIAL") ?? "Ctrl+Alt+Space";

    private static string ChangedHotkey =>
        Environment.GetEnvironmentVariable("AMR_PTT_E2E_CHANGED") ?? "Ctrl+Shift+F9";

    [Fact]
    [Trait("Category", "PttE2e")]
    public async Task Open_change_hotkey_via_settings_save_then_press_starts_recording()
    {
        if (!WindowsKeyboardSimulator.CanSimulateInput())
        {
            return;
        }

        var pressed = new ManualResetEventSlim(false);
        var utteranceReady = new ManualResetEventSlim(false);
        var ready = new ManualResetEventSlim(false);
        var finished = new ManualResetEventSlim(false);
        Exception? fault = null;
        string? activeHotkey = null;
        PttTrayLikeE2eHarness? harnessRef = null;

        var thread = new Thread(() =>
        {
            PttTrayLikeE2eHarness? harness = null;
            try
            {
                ApplicationConfiguration.Initialize();

                var settings = new AppSettings
                {
                    MasterEnabled = true,
                    PromptRefineEnabled = false,
                    TriggerMode = VoiceTriggerMode.PttOnly,
                    PttHotkey = InitialHotkey,
                    ModelsDirectory = Path.Combine(AppContext.BaseDirectory, "models"),
                    ApiBaseUrl = "http://127.0.0.1:11434/v1",
                    PrivacyAcceptedHost = "127.0.0.1",
                    FeaturePresets = [],
                    LlmPresets =
                    [
                        new LlmPreset
                        {
                            Name = "Local",
                            ApiBaseUrl = "http://127.0.0.1:11434/v1",
                            ApiModel = "test",
                        },
                    ],
                };

                harness = new PttTrayLikeE2eHarness(settings);
                harnessRef = harness;
                harness.CaptureService.UtteranceReady += (_, _) => utteranceReady.Set();
                harness.PushToTalk.PttPressed += (_, _) => pressed.Set();

                harness.Show();

                var exitTimer = new System.Windows.Forms.Timer { Interval = 20000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    Application.ExitThread();
                };
                exitTimer.Start();

                // 消息泵已跑起来后再：Idle 注册初始钩子 → 设置页保存新热键 → 通知测试线程按键
                var setupTimer = new System.Windows.Forms.Timer { Interval = 150 };
                setupTimer.Tick += (_, _) =>
                {
                    setupTimer.Stop();
                    try
                    {
                        harness.SaveHotkeyViaSettingsUi(ChangedHotkey);
                        activeHotkey = ChangedHotkey;

                        if (harness.PushToTalk is not NAudioPushToTalkSource { IsRegistered: true })
                        {
                            throw new InvalidOperationException("PTT hook not registered after settings save");
                        }

                        ready.Set();
                    }
                    catch (Exception ex)
                    {
                        fault = ex;
                        ready.Set();
                        Application.ExitThread();
                    }
                };
                setupTimer.Start();

                Application.Run();
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            finally
            {
                harness?.Dispose();
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "PttE2eSettingsChange",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "E2E harness did not become ready");
        Assert.Null(fault);
        Assert.Equal(ChangedHotkey, activeHotkey);

        await Task.Delay(100);

        // 模拟按下「用户在设置里保存的热键」
        WindowsKeyboardSimulator.HoldChordForExpression(activeHotkey!, holdMs: 0);

        Assert.True(
            pressed.Wait(TimeSpan.FromSeconds(4)),
            $"Pressing saved hotkey '{activeHotkey}' did not start PTT capture");

        WindowsKeyboardSimulator.ReleaseHotkeyExpression(activeHotkey!);
        await Task.Delay(300);

        Assert.True(
            utteranceReady.Wait(TimeSpan.FromSeconds(6)),
            "PTT capture did not emit utterance after release");

        harnessRef?.AnchorForm.BeginInvoke(Application.ExitThread);
        Assert.True(finished.Wait(TimeSpan.FromSeconds(12)));
        Assert.Null(fault);
    }
}

#endif
