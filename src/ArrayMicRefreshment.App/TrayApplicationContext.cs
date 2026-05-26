using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Output;
using ArrayMicRefreshment.Prompt;
using ArrayMicRefreshment.Speaker;
using Serilog;

namespace ArrayMicRefreshment.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ISettingsStore _settingsStore = new JsonSettingsStore();
    private AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _masterSwitchItem;
    private readonly ToolStripMenuItem _pasteSwitchItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly IPushToTalkSource _ptt;
    private readonly PttCaptureService _pttCaptureService;
    private readonly VoiceCaptureOrchestrator _voiceOrchestrator;
    private readonly IWakeWordDetector _wakeDetector;
    private VoicePipeline _pipeline;
    private VoiceTriggerMode _voiceTriggerMode = VoiceTriggerMode.PttOnly;
    private SherpaPipelineFactory.PipelineComponents? _sherpaComponents;
    private readonly ClipboardTranscriptSink _sink;
    private readonly TrayBalloonHelper _balloons;
    private readonly VoiceFeedbackPresenter _feedback;
    private readonly SynchronizationContext _uiContext;
    private bool _wakePausedForPtt;
    private nint _settingsWindowHandle;
    private string? _registeredPttHotkey;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _voiceTriggerMode = NormalizePersistedMode(_settings.TriggerMode);
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        if (SynchronizationContext.Current is null)
        {
            SynchronizationContext.SetSynchronizationContext(_uiContext);
        }

        _sink = new ClipboardTranscriptSink(
            () => _settingsWindowHandle,
            _uiContext,
            GetExcludedPasteWindows);

        _masterSwitchItem = new ToolStripMenuItem("启用语音转写", null, OnToggleMaster)
        {
            CheckOnClick = true,
            Checked = _settings.MasterEnabled,
        };
        _pasteSwitchItem = new ToolStripMenuItem("识别后粘贴到光标", null, OnTogglePaste)
        {
            CheckOnClick = true,
            Checked = _settings.PasteToCaretEnabled,
            Enabled = _settings.MasterEnabled,
        };
        _statusItem = new ToolStripMenuItem("状态: 就绪") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_masterSwitchItem);
        menu.Items.Add(_pasteSwitchItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripMenuItem("设置…", null, OnOpenSettings));
        menu.Items.Add(new ToolStripMenuItem("注册说话人…", null, OnEnrollSpeaker));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("强制结束录音（卡住时用）", null, OnSimulatePttRelease));
        menu.Items.Add(BuildTriggerModeMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, OnExit));

        _trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.ForPhase(VoiceActivityPhase.Idle),
            Visible = true,
            Text = "Array Mic Refreshment",
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => OnOpenSettings(null, EventArgs.Empty);
        _balloons = new TrayBalloonHelper(_trayIcon, _uiContext);
        _feedback = new VoiceFeedbackPresenter(_trayIcon, _uiContext, UpdateTrayTooltip);
        _feedback.ApplyHudCorner(_settings.HudScreenCorner);

        _sink.Emitted += (text, paste) =>
            Log.Information("Transcript emitted (paste={Paste}): {Text}", paste, text);

        _ptt = new NAudioPushToTalkSource(_settings.PttHotkey);
        _registeredPttHotkey = _settings.PttHotkey;
        if (_ptt is NAudioPushToTalkSource winPtt)
        {
            winPtt.ForegroundAtPress += RememberPasteTarget;
            winPtt.ForegroundAtRelease += RememberPasteTarget;
        }

        var pttRegistered = _ptt is NAudioPushToTalkSource { IsRegistered: true };
        Log.Information(
            "PTT hotkey loaded from settings: {Saved} → registered as {Active} (ok={Ok})",
            _settings.PttHotkey,
            _ptt.HotkeyDisplay,
            pttRegistered);

        var startupPreset = _settings.CurrentPreset;
        Log.Information(
            "[DIAGNOSTIC] Startup settings: PromptRefineEnabled={RefineEnabled}, " +
            "Preset={PresetName}, Url={ApiUrl}, Model={ApiModel}",
            _settings.PromptRefineEnabled,
            startupPreset.Name,
            startupPreset.ApiBaseUrl,
            startupPreset.ApiModel);

        _pipeline = BuildPipeline(_settings);

        var deviceEnumerator = new NAudioDeviceEnumerator();
        var captureFactory = new NAudioCaptureStreamFactory();
        var vad = new SileroVoiceActivityDetector(_settings.ModelsDirectory);

        _pttCaptureService = new PttCaptureService(
            _settings,
            _ptt,
            deviceEnumerator,
            captureFactory,
            vad,
            pttCaptureAllowed: IsPttCaptureEnabled,
            takePreRollOnPress: TakeWakePreRollForPtt,
            beforeCaptureStarts: PauseWakeListeningForPtt,
            afterCaptureEnds: ResumeWakeListeningAfterPtt);

        _wakeDetector = WakeWordDetectorFactory.Create(_settings);
        Log.Information(
            "[DIAG] Wake detector created: {DetectorId} (phrase={Phrase})",
            _wakeDetector.DetectorId,
            _settings.WakeWordPhrase);
        var wakeCapture = new WakeWordCaptureService(
            _settings,
            _wakeDetector,
            deviceEnumerator,
            captureFactory,
            vad);

        _voiceOrchestrator = new VoiceCaptureOrchestrator(
            _pttCaptureService,
            wakeCapture,
            _voiceTriggerMode);

        _voiceOrchestrator.UtteranceReady += OnUtteranceReady;
        _voiceOrchestrator.CaptureFailed += OnCaptureFailed;
        _voiceOrchestrator.CaptureEmpty += OnCaptureEmpty;
        _voiceOrchestrator.WakeStatusChanged += OnWakeStatusChanged;
        _voiceOrchestrator.WakeWordActivated += OnWakeWordActivated;
        _ptt.PttPressed += OnPttPressed;
        _ptt.PttReleased += OnPttReleased;

        Log.Information("Voice capture orchestrator started (mode={Mode})", _voiceTriggerMode);

#if WINDOWS
        if (_ptt is NAudioPushToTalkSource { IsRegistered: false })
        {
            _statusItem.Text = "状态: PTT 热键未注册";
            _balloons.Show(
                6000,
                "Array Mic",
                "PTT 全局热键注册失败，按键录音不可用。请在设置中更换热键组合。",
                ToolTipIcon.Error);
            Log.Error("PTT RegisterHotKey failed for {Hotkey}", _settings.PttHotkey);
        }
#endif

#if WINDOWS
        var warmTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        warmTimer.Tick += (_, _) =>
        {
            warmTimer.Stop();
            warmTimer.Dispose();
            WarmAudioCaptureIfNeeded();
        };
        warmTimer.Start();
#endif

        UpdateTrayTooltip();
        if (_voiceTriggerMode is VoiceTriggerMode.WakeWordOnly or VoiceTriggerMode.Both)
        {
            NotifyWakeModeStartup();
        }

#if WINDOWS
        ApplyLaunchAtStartup(_settings.LaunchAtStartup);
#endif
    }

#if WINDOWS
    private static void ApplyLaunchAtStartup(bool enabled)
    {
        try
        {
            WindowsStartupRegistration.Apply(enabled);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply launch-at-startup setting (enabled={Enabled})", enabled);
        }
    }
#endif

    private VoicePipeline BuildPipeline(AppSettings settings)
    {
        _sherpaComponents?.DisposeOwned();
        _sherpaComponents = SherpaPipelineFactory.CreateOrFallback(settings, _settingsStore);
        if (_sherpaComponents.AsrModelsMissing)
        {
            _statusItem.Text = "状态: ASR 模型缺失 — 运行 download-models.ps1";
        }
        else if (_sherpaComponents.SpeakerModelMissing)
        {
            _statusItem.Text = "状态: 说话人模型缺失（声纹校验未启用）";
        }

        var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(settings.SkillsDirectory));
        if (catalog.MissingFiles.Count > 0)
        {
            Log.Warning("Missing skill files: {Files}", string.Join(", ", catalog.MissingFiles));
        }

        var router = new OpenAiCompatibleIntentRouter(settings, catalog);
        var refiner = new OpenAiCompatiblePromptRefiner(settings, catalog);
        Log.Information(
            "[DIAGNOSTIC] BuildPipeline: PromptRefineEnabled={RefineEnabled}, " +
            "RefinerType={RefinerType}, RefinerIsEnabled={RefinerEnabled}",
            settings.PromptRefineEnabled,
            refiner.GetType().Name,
            refiner.IsEnabled);
        return new VoicePipeline(
            settings,
            _sherpaComponents.Speaker,
            _sherpaComponents.Asr,
            router,
            refiner,
            _sink);
    }

    private void OnToggleMaster(object? sender, EventArgs e)
    {
        _settings.MasterEnabled = _masterSwitchItem.Checked;
        _pasteSwitchItem.Enabled = _settings.MasterEnabled;
        PersistAndRefresh();
    }

    private void OnTogglePaste(object? sender, EventArgs e)
    {
        _settings.PasteToCaretEnabled = _pasteSwitchItem.Checked;
        PersistAndRefresh();
    }

    private void OnEnrollSpeaker(object? sender, EventArgs e)
    {
        if (_sherpaComponents?.Speaker is not SpeakerGate gate)
        {
            _balloons.Show(
                6000,
                "Array Mic",
                "说话人模型未加载。请在 exe 旁放置 models，并运行 scripts\\download-models.ps1（默认会下载声纹模型）。",
                ToolTipIcon.Warning);
            return;
        }

#if WINDOWS
        using var capture = new EnrollmentUtteranceCapture(
            _settings,
            new NAudioDeviceEnumerator(),
            new NAudioCaptureStreamFactory());
        using var dialog = new EnrollmentDialog(gate.Enrollment, capture);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _settings.CurrentSpeakerUserId = gate.Enrollment.CurrentUserId;
            _settingsStore.Save(_settings);
            _balloons.Show(
                5000,
                "Array Mic",
                "说话人注册成功。请在「设置」中将「当前用户」选为刚注册的用户以启用声纹校验。",
                ToolTipIcon.Info);
        }
#else
        _ = sender;
#endif
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        IAudioDeviceEnumerator? deviceEnumerator = null;
        IEnrollmentUtteranceSource? enrollmentCapture = null;
#if WINDOWS
        deviceEnumerator = new NAudioDeviceEnumerator();
        enrollmentCapture = new EnrollmentUtteranceCapture(
            _settings,
            deviceEnumerator,
            new NAudioCaptureStreamFactory());
#endif

        IUserEnrollmentService? enrollment = null;
        if (_sherpaComponents?.Speaker is SpeakerGate gate)
        {
            enrollment = gate.Enrollment;
        }

        using var form = new SettingsForm(
            _settings,
            deviceEnumerator,
            enrollment,
            enrollmentCapture,
            _sherpaComponents?.SpeakerModelMissing == true)
        {
            RuntimeTriggerMode = _voiceTriggerMode,
        };
        form.Shown += (_, _) => _settingsWindowHandle = form.Handle;
        if (form.ShowDialog() == DialogResult.OK)
        {
            var previous = CloneSettingsSnapshot(_settings);
            SettingsCopier.CopyInto(form.Settings, _settings);

            var rebuildRequired = SettingsCopier.RequiresPipelineRebuild(previous, _settings);
            Log.Information(
                "[DIAGNOSTIC] Settings changed. PreviousRefine={PrevRefine}, CurrentRefine={CurrRefine}, " +
                "RebuildRequired={Rebuild}",
                previous.PromptRefineEnabled,
                _settings.PromptRefineEnabled,
                rebuildRequired);

            if (rebuildRequired)
            {
                Log.Information("Pipeline rebuild required. Rebuilding...");
                _pipeline = BuildPipeline(_settings);
            }
            else
            {
                _pipeline.ApplySettings(_settings);
            }

            var hotkeyChanged = !string.Equals(
                _registeredPttHotkey,
                _settings.PttHotkey,
                StringComparison.OrdinalIgnoreCase);
            if (hotkeyChanged
                && _ptt is NAudioPushToTalkSource naudioPtt)
            {
                if (!naudioPtt.TryUpdateHotkey(_settings.PttHotkey, out var hotkeyError))
                {
                    MessageBox.Show(hotkeyError ?? "PTT 热键注册失败。", "热键", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    _registeredPttHotkey = _settings.PttHotkey;
                    _balloons.Show(
                        4000,
                        "Array Mic",
                        $"PTT 热键已更新为 {naudioPtt.HotkeyDisplay}",
                        ToolTipIcon.Info);
                }
            }

            var persistedMode = NormalizePersistedMode(_settings.TriggerMode);
            if (_voiceTriggerMode != persistedMode)
            {
                SetVoiceTriggerMode(persistedMode);
            }

            if (!string.Equals(previous.WakeWordPhrase, _settings.WakeWordPhrase, StringComparison.Ordinal))
            {
                try
                {
                    _wakeDetector.ApplyPhrase(_settings.WakeWordPhrase);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to apply wake phrase after settings save");
                    MessageBox.Show(
                        $"唤醒词配置更新失败：{ex.Message}",
                        "唤醒词",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            if (previous.WakeWordSensitivity != _settings.WakeWordSensitivity)
            {
                _wakeDetector.ApplyWakeSensitivity(_settings.WakeWordSensitivity);
            }

            if (previous.WakeCommandSilenceMs != _settings.WakeCommandSilenceMs
                && _voiceOrchestrator.WakeCapture is WakeWordCaptureService wakeCaptureService)
            {
                wakeCaptureService.ApplyWakeCaptureSettings(_settings);
            }

            if (previous.HudScreenCorner != _settings.HudScreenCorner)
            {
                _feedback.ApplyHudCorner(_settings.HudScreenCorner);
            }

            if (previous.LaunchAtStartup != _settings.LaunchAtStartup)
            {
                ApplyLaunchAtStartup(_settings.LaunchAtStartup);
            }

            _pttCaptureService.InvalidateCaptureDevice();
            PersistAndRefresh();
#if WINDOWS
            if (SettingsCopier.RequiresWakeCaptureRestart(previous, _settings))
            {
                RefreshAudioCaptureAfterSettings();
            }
#endif
        }

#if WINDOWS
        if (enrollmentCapture is IDisposable disposableCapture)
        {
            disposableCapture.Dispose();
        }
#endif

        _settingsWindowHandle = IntPtr.Zero;
    }

    private void RememberPasteTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || IsExcludedPasteRoot(hwnd))
        {
            return;
        }

        var focus = WindowsPasteHelper.ResolveFocusHwnd(hwnd);
        _sink.SetPasteTarget(hwnd, focus);
    }

    private void CapturePasteTargetFromForeground()
    {
        var (root, focus) = WindowsPasteHelper.CaptureForegroundExcluding(GetExcludedPasteWindows());
        if (root == IntPtr.Zero)
        {
            Log.Debug("CapturePasteTargetFromForeground: no eligible foreground window");
            return;
        }

        _sink.SetPasteTarget(root, focus);
        Log.Debug("CapturePasteTargetFromForeground: root={Root:X} focus={Focus:X}", root, focus);
    }

    private IReadOnlyList<IntPtr> GetExcludedPasteWindows()
    {
        var excluded = new List<IntPtr>(2);
        if (_settingsWindowHandle != IntPtr.Zero)
        {
            excluded.Add(_settingsWindowHandle);
        }

        var hud = _feedback.HudHandle;
        if (hud != IntPtr.Zero)
        {
            excluded.Add(hud);
        }

        return excluded;
    }

    private bool IsExcludedPasteRoot(IntPtr hwnd)
        => WindowsPasteHelper.IsExcludedWindow(hwnd, GetExcludedPasteWindows());

    private ToolStripMenuItem BuildTriggerModeMenu()
    {
        var root = new ToolStripMenuItem("触发模式（运行时）");
        root.DropDownItems.Add(CreateModeItem("仅 PTT", VoiceTriggerMode.PttOnly));
        root.DropDownItems.Add(CreateModeItem("仅唤醒词", VoiceTriggerMode.WakeWordOnly));
        root.DropDownItems.Add(CreateModeItem("PTT + 唤醒词", VoiceTriggerMode.Both));
        root.DropDownItems.Add(new ToolStripSeparator());
        root.DropDownItems.Add(new ToolStripMenuItem(
            "模拟唤醒（Stub 检测器）",
            null,
            OnSimulateWakeWord));
        return root;
    }

    private ToolStripMenuItem CreateModeItem(string label, VoiceTriggerMode mode)
    {
        return new ToolStripMenuItem(label, null, (_, _) => SetVoiceTriggerMode(mode))
        {
            Checked = _voiceTriggerMode == mode,
        };
    }

    /// <summary>Runtime mode switch (settings UI will persist this later).</summary>
    public void SetVoiceTriggerMode(VoiceTriggerMode mode)
    {
        if (_voiceTriggerMode == mode)
        {
            return;
        }

        _voiceTriggerMode = mode;
        _settings.TriggerMode = mode;
        _voiceOrchestrator.SetMode(mode);
        RefreshTriggerModeMenuChecks();
        UpdateTrayTooltip();
        PersistAndRefresh();

        var status = mode switch
        {
            VoiceTriggerMode.PttOnly => "状态: 就绪（PTT）",
            VoiceTriggerMode.WakeWordOnly => "状态: 监听唤醒词…",
            VoiceTriggerMode.Both => "状态: PTT + 监听唤醒词",
            _ => "状态: 就绪",
        };
        _statusItem.Text = status;
        if (mode is VoiceTriggerMode.PttOnly)
        {
            _feedback.ClearSession();
        }

        Log.Information("Voice trigger mode changed to {Mode}", mode);

        if (mode is VoiceTriggerMode.WakeWordOnly or VoiceTriggerMode.Both)
        {
            NotifyWakeModeStartup();
        }
    }

    private void NotifyWakeModeStartup()
    {
        if (_wakeDetector.DetectorId.StartsWith("stub", StringComparison.OrdinalIgnoreCase))
        {
            _balloons.Show(
                8000,
                "Array Mic — 唤醒词",
                "未检测到 Sherpa 唤醒模型，设置里的唤醒词暂无法从语音识别。\n" +
                "请在 exe 旁运行：scripts\\download-models.ps1 -IncludeKws\n" +
                "安装前可用托盘「模拟唤醒」测试后续流程。",
                ToolTipIcon.Warning);
        }
        else
        {
            Log.Information("Wake-word listening active for phrase {Phrase}", _settings.WakeWordPhrase);
        }
    }

    private void OnWakeWordActivated(object? sender, WakeWordDetectedEventArgs e)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        Log.Information("Wake-word activated UI notification for {Keyword}", e.Keyword);
        RunOnUiSync(() =>
        {
            CapturePasteTargetFromForeground();
            _statusItem.Text = "状态: 识别中…";
            _feedback.SetPhase(
                VoiceActivityPhase.WakePrompt,
                $"已唤醒 — 请说指令");
        });
    }

    private bool IsPttCaptureEnabled() =>
        _voiceTriggerMode is VoiceTriggerMode.PttOnly or VoiceTriggerMode.Both;

    private void WarmAudioCaptureIfNeeded()
    {
        if (_voiceTriggerMode is VoiceTriggerMode.PttOnly or VoiceTriggerMode.Both)
        {
            _pttCaptureService.WarmCaptureDevice();
        }
    }

    private void RefreshAudioCaptureAfterSettings()
    {
        if (_voiceTriggerMode == VoiceTriggerMode.PttOnly)
        {
            _pttCaptureService.WarmCaptureDevice();
            return;
        }

        Log.Information("Restarting wake-word listening after settings (mode={Mode})", _voiceTriggerMode);
        if (_voiceOrchestrator.WakeCapture is WakeWordCaptureService wakeCapture)
        {
            wakeCapture.RestartListening();
        }
        else
        {
            _voiceOrchestrator.WakeCapture.StopListening();
            _voiceOrchestrator.WakeCapture.StartListening();
        }
    }

    private void PauseWakeListeningForPtt()
    {
        var wake = _voiceOrchestrator.WakeCapture;
        if (!wake.IsListening && !wake.IsDictationActive)
        {
            return;
        }

        Log.Information("Releasing wake mic for PTT (mode={Mode})", _voiceTriggerMode);
        wake.ReleaseMicForPtt();
        _wakePausedForPtt = _voiceTriggerMode == VoiceTriggerMode.Both;
    }

    private byte[]? TakeWakePreRollForPtt()
    {
        if (_voiceTriggerMode != VoiceTriggerMode.Both)
        {
            return null;
        }

        if (_voiceOrchestrator.WakeCapture is WakeWordCaptureService wakeCapture)
        {
            var preRoll = wakeCapture.TakePreRollSnapshotForPttHandoff();
            return preRoll.Length > 0 ? preRoll : null;
        }

        return null;
    }

    private void ResumeWakeListeningAfterPtt()
    {
        if (!_wakePausedForPtt)
        {
            return;
        }

        _wakePausedForPtt = false;
        if (_voiceTriggerMode is VoiceTriggerMode.WakeWordOnly or VoiceTriggerMode.Both)
        {
            Log.Information("Resuming wake-word listening after PTT");
            _voiceOrchestrator.WakeCapture.StartListening();
        }
    }

    private void RunOnUi(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }

    private void RunOnUiSync(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Send(_ => action(), null);
    }

    private static VoiceTriggerMode NormalizePersistedMode(VoiceTriggerMode mode) =>
        mode switch
        {
            VoiceTriggerMode.WakeWordOnly => VoiceTriggerMode.WakeWordOnly,
            VoiceTriggerMode.Both => VoiceTriggerMode.Both,
            _ => VoiceTriggerMode.PttOnly,
        };

    private void RefreshTriggerModeMenuChecks()
    {
        if (_trayIcon.ContextMenuStrip is null)
        {
            return;
        }

        foreach (ToolStripItem item in _trayIcon.ContextMenuStrip.Items)
        {
            if (item is not ToolStripMenuItem { Text: "触发模式（运行时）" } root)
            {
                continue;
            }

            foreach (ToolStripItem sub in root.DropDownItems)
            {
                if (sub is not ToolStripMenuItem mi || mi.Text.StartsWith("模拟", StringComparison.Ordinal))
                {
                    continue;
                }

                mi.Checked = mi.Text switch
                {
                    "仅 PTT" => _voiceTriggerMode == VoiceTriggerMode.PttOnly,
                    "仅唤醒词" => _voiceTriggerMode == VoiceTriggerMode.WakeWordOnly,
                    "PTT + 唤醒词" => _voiceTriggerMode == VoiceTriggerMode.Both,
                    _ => false,
                };
            }
        }
    }

    private void OnSimulateWakeWord(object? sender, EventArgs e)
    {
        if (_voiceTriggerMode == VoiceTriggerMode.PttOnly)
        {
            _balloons.Show(
                4000,
                "Array Mic",
                "当前为「仅 PTT」模式。请先在「触发模式」中切换到唤醒词相关模式。",
                ToolTipIcon.Info);
            return;
        }

        if (!_settings.MasterEnabled)
        {
            return;
        }

        Log.Information("Simulating wake-word detection (stub detector)");
        if (_wakeDetector is StubWakeWordDetector stub)
        {
            stub.SimulateDetection(_settings.WakeWordPhrase);
        }
        else
        {
            _balloons.Show(
                4000,
                "Array Mic",
                "当前为 Sherpa 唤醒检测器，请直接说出设置的唤醒词。",
                ToolTipIcon.Info);
        }
    }

    private void OnWakeStatusChanged(object? sender, string status)
    {
        if (_voiceTriggerMode == VoiceTriggerMode.PttOnly)
        {
            return;
        }

        if (!_pttCaptureService.IsPttHeld)
        {
            RunOnUi(() =>
            {
                _statusItem.Text = $"状态: {status}";
                ApplyWakeStatusFeedback(status);
            });
        }
    }

    private void ApplyWakeStatusFeedback(string status)
    {
        switch (status)
        {
            case "识别中…":
                _feedback.SetPhase(
                    VoiceActivityPhase.WakePrompt,
                    $"已唤醒 — 请说指令");
                break;
            case "正在聆听指令…":
                CapturePasteTargetFromForeground();
                _feedback.SetPhase(VoiceActivityPhase.Recording, "聆听中…");
                break;
            case "唤醒句段已提交识别":
                _feedback.SetPhase(VoiceActivityPhase.Recognizing, "识别中…");
                break;
            case "监听唤醒词…":
            case "唤醒监听已停止":
                _feedback.ClearSession();
                break;
        }
    }

    private void OnPttPressed(object? sender, EventArgs e)
    {
        Log.Information("PTT pressed ({Hotkey})", _ptt.HotkeyDisplay);
        _balloons.DismissForNextTask();
        RunOnUi(() =>
        {
            if (_voiceTriggerMode == VoiceTriggerMode.WakeWordOnly)
            {
                _statusItem.Text = "状态: 仅唤醒词（PTT 未启用）";
                _balloons.Show(
                    4000,
                    "Array Mic",
                    "当前为「仅唤醒词」模式，PTT 不会录音。\n" +
                    "请用托盘「触发模式」切换到「仅 PTT」或「PTT + 唤醒词」。",
                    ToolTipIcon.Info);
                return;
            }

            _statusItem.Text = "状态: 录音中…";
            _feedback.SetPhase(VoiceActivityPhase.Recording, "录音中…");
        });
    }

    private void OnPttReleased(object? sender, EventArgs e)
    {
        Log.Information("PTT released — waiting for utterance");
    }

    private async void OnSimulatePttRelease(object? sender, EventArgs e)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        try
        {
            if (_pttCaptureService.IsPttHeld)
            {
                _statusItem.Text = "状态: 识别中…";
                _feedback.SetPhase(VoiceActivityPhase.Recognizing, "识别中…");
                Log.Information("Simulate PTT release (finalize held recording)");
                if (_ptt is NAudioPushToTalkSource naudio)
                {
                    naudio.SimulateRelease();
                }
                else if (_ptt is StubPushToTalkSource stub)
                {
                    stub.SimulateRelease();
                }
                else
                {
                    _pttCaptureService.SimulateReleaseForDev();
                }

                await Task.Delay(300).ConfigureAwait(true);
            }
            else
            {
                _balloons.Show(
                    4000,
                    "Array Mic",
                    "当前未在录音。请先按住 PTT 热键说话，松开后应自动识别；本菜单仅在卡住时用于强制结束录音。",
                    ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Simulate PTT release failed");
            await RunDevFallbackUtteranceAsync().ConfigureAwait(true);
        }

        if (_statusItem.Text?.StartsWith("状态: 录音", StringComparison.Ordinal) == true)
        {
            _statusItem.Text = "状态: 就绪";
        }
    }

    private void OnCaptureFailed(object? sender, Exception ex)
    {
        Log.Warning(ex, "PTT capture failed");
        RunOnUi(() =>
        {
            _statusItem.Text = "状态: 录音失败";
            _feedback.SetPhase(VoiceActivityPhase.Error, "录音失败");
            _balloons.Show(
                4000,
                "Array Mic",
                ex.Message,
                ToolTipIcon.Warning);
        });
    }

    private void OnCaptureEmpty(object? sender, string message)
    {
        Log.Warning("Capture empty: {Message}", message);
        RunOnUi(() =>
        {
            _statusItem.Text = _voiceTriggerMode == VoiceTriggerMode.PttOnly
                ? "状态: 未录到音频"
                : "状态: 监听唤醒词…";
            _feedback.SetPhase(VoiceActivityPhase.Error, message);
            _balloons.Show(
                4000,
                "Array Mic",
                message,
                ToolTipIcon.Warning);
            _feedback.ClearSession();
        });
    }

    private async void OnUtteranceReady(object? sender, UtteranceCaptureEventArgs e)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        var utterance = e.Utterance;
        Log.Information(
            "Utterance ready ({Trigger}): {Ms} ms, {Bytes} bytes",
            e.TriggerKind,
            utterance.Duration.TotalMilliseconds,
            utterance.Pcm16LeMono.Length);

        if (_sherpaComponents?.AsrModelsMissing == true)
        {
            RunOnUi(() =>
            {
                _statusItem.Text = "状态: ASR 模型缺失";
                _feedback.SetPhase(VoiceActivityPhase.Error, "ASR 模型缺失");
                _balloons.Show(
                    8000,
                    "Array Mic",
                    "未找到 SenseVoice 模型，无法语音转写。请将 models 文件夹放在 exe 同级目录（在仓库根目录运行 scripts\\download-models.ps1 下载）。",
                    ToolTipIcon.Error);
                _feedback.ClearSession();
            });
            Log.Warning("Skipping ASR: SenseVoice models not found under {ModelsDir}", _settings.ModelsDirectory);
            return;
        }

        RunOnUi(() =>
        {
            _statusItem.Text = "状态: 识别中…";
            _feedback.SetPhase(VoiceActivityPhase.Recognizing, "识别中…");
        });

        Log.Information(
            "[DIAGNOSTIC] Before pipeline processing. " +
            "MasterEnabled={Master}, PromptRefineEnabled={Refine}, " +
            "PipelineType={PipelineType}",
            _settings.MasterEnabled,
            _settings.PromptRefineEnabled,
            _pipeline.GetType().Name);

        VoicePipelineOutcome outcome;
        try
        {
            outcome = await _pipeline.ProcessUtteranceAsync(
                utterance,
                CancellationToken.None,
                e.TriggerKind).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                _statusItem.Text = "状态: 处理异常";
                _feedback.SetPhase(VoiceActivityPhase.Error, "处理失败");
                _balloons.Show(
                    8000,
                    "Array Mic",
                    $"处理失败: {ex.Message}\n\n请查看日志并重启软件。",
                    ToolTipIcon.Error);
                _feedback.ClearSession();
            });
            Log.Error(ex, "Pipeline crashed in OnUtteranceReady");
            return;
        }

        var capturedOutcome = outcome;
        var capturedTrigger = e.TriggerKind;
        RunOnUi(() => ApplyUtteranceOutcome(capturedOutcome, capturedTrigger));
    }

    private void ApplyUtteranceOutcome(VoicePipelineOutcome outcome, VoiceTriggerKind triggerKind)
    {
        var modelLabel = string.IsNullOrEmpty(outcome.AsrModelId)
            ? "未知模型"
            : (outcome.AsrModelId.Contains("2025-09") ? "2025-09(无标点)" :
               outcome.AsrModelId.Contains("int8-2024-07") ? "2024-07-int8(有标点)" :
               outcome.AsrModelId.Contains("2024-07") ? "2024-07-float32(有标点)" :
               outcome.AsrModelId);
        var skillName = SkillLabel(_settings.ForcedIntent);
        var refineLabel = $"{skillName} | {outcome.RefineStatus ?? "未知"}";
        var wakeFlow = triggerKind == VoiceTriggerKind.WakeWord;

        switch (outcome.Status)
        {
            case VoicePipelineStatus.SpeakerRejected:
            case VoicePipelineStatus.EmptyTranscript:
                _statusItem.Text = wakeFlow ? "状态: 监听唤醒词…" : "状态: 未输出";
                Log.Warning("Pipeline outcome: {Status} — {Detail}", outcome.Status, outcome.Detail);
                _feedback.SetPhase(VoiceActivityPhase.Error, outcome.Detail ?? outcome.Status.ToString());
                _balloons.Show(
                    5000,
                    "Array Mic",
                    outcome.Detail ?? outcome.Status.ToString(),
                    ToolTipIcon.Warning);
                _feedback.ClearSession();
                break;
            case VoicePipelineStatus.Emitted:
            case VoicePipelineStatus.EmittedRawFallback:
                _statusItem.Text = wakeFlow
                    ? "状态: 监听唤醒词…"
                    : outcome.Status == VoicePipelineStatus.EmittedRawFallback
                        ? "状态: 整理失败，使用原文"
                        : "状态: 就绪";
                _feedback.ClearSession();
                if (!string.IsNullOrWhiteSpace(outcome.Detail))
                {
                    var preview = outcome.Detail.Length > 40
                        ? outcome.Detail[..40] + "…"
                        : outcome.Detail;
                    var via = _settings.PasteToCaretEnabled ? "已输出" : "已复制到剪贴板";
                    Log.Information("Transcript {Via} (model={Model}, refine={Refine}): {Text}",
                        via, modelLabel, refineLabel, outcome.Detail);

                    string balloonTitle;
                    string balloonBody;
                    if (outcome.Status == VoicePipelineStatus.EmittedRawFallback)
                    {
                        var failureReason = outcome.RefineStatus ?? "未知原因";
                        balloonTitle = $"Array Mic — {modelLabel} | 整理失败";
                        balloonBody = $"原因: {failureReason}\n{via}（原文）: {preview}";
                    }
                    else
                    {
                        balloonTitle = $"Array Mic — {modelLabel} | {refineLabel}";
                        balloonBody = $"{via}: {preview}";
                    }

                    _balloons.Show(
                        5000,
                        balloonTitle,
                        balloonBody,
                        outcome.Status == VoicePipelineStatus.EmittedRawFallback
                            ? ToolTipIcon.Warning
                            : ToolTipIcon.Info);
                }

                break;
            default:
                _statusItem.Text = "状态: 就绪";
                _feedback.ClearSession();
                break;
        }
    }

    private async Task RunDevFallbackUtteranceAsync()
    {
        var utterance = new AudioUtterance
        {
            Pcm16LeMono = new byte[3200],
            SampleRate = 16000,
            Duration = TimeSpan.FromMilliseconds(200),
        };
        _ = await _pipeline.ProcessUtteranceAsync(utterance, CancellationToken.None).ConfigureAwait(true);
    }

    private static AppSettings CloneSettingsSnapshot(AppSettings source)
    {
        var clone = new AppSettings();
        SettingsCopier.CopyInto(source, clone);
        return clone;
    }

    private void PersistAndRefresh()
    {
        _settingsStore.Save(_settings);
        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        var master = _settings.MasterEnabled ? "开" : "关";
        var pttOk = _ptt is not NAudioPushToTalkSource naudio || naudio.IsRegistered;
        var pttLabel = _ptt.HotkeyDisplay;
        var ptt = pttOk ? pttLabel : $"{pttLabel}?";
        var modeLabel = _voiceTriggerMode switch
        {
            VoiceTriggerMode.WakeWordOnly => "唤醒",
            VoiceTriggerMode.Both => "PTT+唤醒",
            _ => "PTT",
        };
        var models = _sherpaComponents?.AsrModelsMissing == true
            ? " ASR缺失"
            : _sherpaComponents?.SpeakerModelMissing == true
                ? " 无声纹"
                : string.Empty;
        var activity = string.IsNullOrEmpty(_feedback.ActivityHint) ? string.Empty : $" {_feedback.ActivityHint}";
        _trayIcon.Text = $"Array Mic {AppInfo.Version} — {modeLabel}{activity} {ptt} 转写:{master}{models}";
        if (_trayIcon.Text.Length > 63)
        {
            _trayIcon.Text = _trayIcon.Text[..63];
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _voiceOrchestrator.Dispose();
        _pttCaptureService.Dispose();
        if (_ptt is IDisposable d)
        {
            d.Dispose();
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _feedback.Dispose();
        ExitThread();
    }

    private static string SkillLabel(PromptIntent intent) => intent switch
    {
        PromptIntent.PlainText => "纯文本整理",
        PromptIntent.GeneralAi => "通用 AI Prompt",
        PromptIntent.CodeEditing => "代码编辑指令",
        PromptIntent.Research => "深度研究",
        PromptIntent.TaskPlan => "待办列表",
        _ => "自动判断",
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _voiceOrchestrator.Dispose();
            _pttCaptureService.Dispose();
            if (_ptt is IDisposable d)
            {
                d.Dispose();
            }

            _sherpaComponents?.DisposeOwned();
            _feedback.Dispose();
            _balloons.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
