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
    private readonly PttCaptureService _captureService;
    private VoicePipeline _pipeline;
    private SherpaPipelineFactory.PipelineComponents? _sherpaComponents;
    private readonly ClipboardTranscriptSink _sink;
    private nint _settingsWindowHandle;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load();
        _sink = new ClipboardTranscriptSink(() => _settingsWindowHandle);
        _pipeline = BuildPipeline(_settings);

        _ptt = new NAudioPushToTalkSource(_settings.PttHotkey);
        _captureService = new PttCaptureService(
            _settings,
            _ptt,
            new NAudioDeviceEnumerator(),
            new NAudioCaptureStreamFactory(),
            new SileroVoiceActivityDetector(_settings.ModelsDirectory));
        _captureService.UtteranceReady += OnUtteranceReady;

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
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("模拟松开 PTT（开发）", null, OnSimulatePttRelease));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, OnExit));

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Array Mic Refreshment",
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => OnOpenSettings(null, EventArgs.Empty);

        _sink.Emitted += (text, paste) =>
            Log.Information("Transcript emitted (paste={Paste}): {Text}", paste, text);

        UpdateTrayTooltip();
    }

    private VoicePipeline BuildPipeline(AppSettings settings)
    {
        _sherpaComponents?.DisposeOwned();
        _sherpaComponents = SherpaPipelineFactory.CreateOrFallback(settings, _settingsStore);
        if (_sherpaComponents.ModelsMissing)
        {
            _statusItem.Text = "状态: 模型缺失 — 运行 download-models.ps1";
            _trayIcon.Text = "Array Mic — 模型缺失";
        }
        else
        {
            UpdateTrayTooltip();
        }

        var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(settings.SkillsDirectory));
        if (catalog.MissingFiles.Count > 0)
        {
            Log.Warning("Missing skill files: {Files}", string.Join(", ", catalog.MissingFiles));
        }

        var router = new OpenAiCompatibleIntentRouter(settings, catalog);
        var refiner = new OpenAiCompatiblePromptRefiner(settings, catalog);
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

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_settings);
        form.Shown += (_, _) => _settingsWindowHandle = form.Handle;
        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.Settings;
            _pipeline = BuildPipeline(_settings);
            if (_ptt is NAudioPushToTalkSource naudioPtt)
            {
                naudioPtt.UpdateHotkey(_settings.PttHotkey);
            }

            PersistAndRefresh();
        }

        _settingsWindowHandle = IntPtr.Zero;
    }

    private async void OnSimulatePttRelease(object? sender, EventArgs e)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        _statusItem.Text = "状态: 识别中…";
        try
        {
            if (_ptt is NAudioPushToTalkSource naudio)
            {
                naudio.SimulatePress();
                await Task.Delay(80).ConfigureAwait(true);
                naudio.SimulateRelease();
                await Task.Delay(150).ConfigureAwait(true);
            }
            else if (_ptt is StubPushToTalkSource stub)
            {
                stub.SimulatePress();
                stub.SimulateRelease();
            }
            else
            {
                await RunDevFallbackUtteranceAsync().ConfigureAwait(true);
            }
        }
        catch
        {
            await RunDevFallbackUtteranceAsync().ConfigureAwait(true);
        }

        _statusItem.Text = "状态: 就绪";
    }

    private async void OnUtteranceReady(object? sender, AudioUtterance utterance)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        _statusItem.Text = "状态: 识别中…";
        try
        {
            await _pipeline.ProcessUtteranceAsync(utterance, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _statusItem.Text = "状态: 就绪";
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
        await _pipeline.ProcessUtteranceAsync(utterance, CancellationToken.None).ConfigureAwait(true);
    }

    private void PersistAndRefresh()
    {
        _settingsStore.Save(_settings);
        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        var master = _settings.MasterEnabled ? "开" : "关";
        var paste = _settings.PasteToCaretEnabled ? "开" : "关";
        var refine = _settings.PromptRefineEnabled ? "开" : "关";
        _trayIcon.Text = $"Array Mic — 转写:{master} 粘贴:{paste} 整理:{refine}";
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _captureService.Dispose();
        if (_ptt is IDisposable d)
        {
            d.Dispose();
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captureService.Dispose();
            if (_ptt is IDisposable d)
            {
                d.Dispose();
            }

            _sherpaComponents?.DisposeOwned();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
