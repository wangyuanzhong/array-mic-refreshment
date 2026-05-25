using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Routes capture events to the shared pipeline by <see cref="VoiceTriggerMode"/>.
/// PTT path is unchanged; wake-word path is optional and can be toggled at runtime.
/// </summary>
public sealed class VoiceCaptureOrchestrator : IDisposable
{
    private readonly PttCaptureService _pttCapture;
    private readonly IWakeWordCaptureService _wakeCapture;
    private readonly object _modeGate = new();
    private VoiceTriggerMode _mode = VoiceTriggerMode.PttOnly;

    public VoiceCaptureOrchestrator(
        PttCaptureService pttCapture,
        IWakeWordCaptureService wakeCapture,
        VoiceTriggerMode initialMode = VoiceTriggerMode.PttOnly)
    {
        _pttCapture = pttCapture;
        _wakeCapture = wakeCapture;
        _mode = initialMode;

        _pttCapture.UtteranceReady += OnPttUtteranceReady;
        _pttCapture.CaptureFailed += OnPttCaptureFailed;
        _pttCapture.CaptureEmpty += OnPttCaptureEmpty;

        _wakeCapture.UtteranceReady += OnWakeUtteranceReady;
        _wakeCapture.CaptureFailed += OnWakeCaptureFailed;
        _wakeCapture.CaptureEmpty += OnWakeCaptureEmpty;
        _wakeCapture.StatusChanged += OnWakeStatusChanged;

        ApplyModeInternal(_mode, log: false);
    }

    public event EventHandler<UtteranceCaptureEventArgs>? UtteranceReady;

    public event EventHandler<Exception>? CaptureFailed;

    public event EventHandler<string>? CaptureEmpty;

    public event EventHandler<string>? WakeStatusChanged;

    public VoiceTriggerMode Mode
    {
        get
        {
            lock (_modeGate)
            {
                return _mode;
            }
        }
    }

    public PttCaptureService PttCapture => _pttCapture;

    public IWakeWordCaptureService WakeCapture => _wakeCapture;

    public void SetMode(VoiceTriggerMode mode)
    {
        lock (_modeGate)
        {
            if (_mode == mode)
            {
                return;
            }

            ApplyModeInternal(mode, log: true);
        }
    }

    private void ApplyModeInternal(VoiceTriggerMode mode, bool log)
    {
        _mode = mode;

        switch (mode)
        {
            case VoiceTriggerMode.PttOnly:
                _wakeCapture.StopListening();
                break;
            case VoiceTriggerMode.WakeWordOnly:
                _wakeCapture.StartListening();
                break;
            case VoiceTriggerMode.Both:
                _wakeCapture.StartListening();
                break;
        }

        if (log)
        {
            Log.Information("Voice trigger mode set to {Mode}", mode);
        }
    }

    private void OnPttUtteranceReady(object? sender, AudioUtterance utterance)
    {
        if (!ShouldForwardPtt())
        {
            Log.Debug("PTT utterance dropped (mode={Mode})", _mode);
            return;
        }

        Log.Information("Orchestrator forwarding PTT utterance ({Bytes} bytes)", utterance.Pcm16LeMono.Length);
        UtteranceReady?.Invoke(
            this,
            new UtteranceCaptureEventArgs(utterance, VoiceTriggerKind.Ptt));
    }

    private void OnWakeUtteranceReady(object? sender, UtteranceCaptureEventArgs e)
    {
        if (!ShouldForwardWake())
        {
            Log.Debug("Wake utterance dropped (mode={Mode})", _mode);
            return;
        }

        if (_mode == VoiceTriggerMode.Both && _pttCapture.IsPttHeld)
        {
            Log.Information("Wake utterance dropped: PTT held (priority)");
            return;
        }

        Log.Information(
            "Orchestrator forwarding wake utterance ({Bytes} bytes)",
            e.Utterance.Pcm16LeMono.Length);
        UtteranceReady?.Invoke(this, e);
    }

    private void OnPttCaptureFailed(object? sender, Exception ex)
    {
        if (ShouldForwardPtt())
        {
            CaptureFailed?.Invoke(this, ex);
        }
    }

    private void OnPttCaptureEmpty(object? sender, string message)
    {
        if (ShouldForwardPtt())
        {
            CaptureEmpty?.Invoke(this, message);
        }
    }

    private void OnWakeCaptureFailed(object? sender, Exception ex)
    {
        if (ShouldForwardWake())
        {
            CaptureFailed?.Invoke(this, ex);
        }
    }

    private void OnWakeCaptureEmpty(object? sender, string message)
    {
        if (ShouldForwardWake())
        {
            CaptureEmpty?.Invoke(this, message);
        }
    }

    private void OnWakeStatusChanged(object? sender, string status)
    {
        if (ShouldForwardWake())
        {
            WakeStatusChanged?.Invoke(this, status);
        }
    }

    private bool ShouldForwardPtt()
    {
        lock (_modeGate)
        {
            return _mode is VoiceTriggerMode.PttOnly or VoiceTriggerMode.Both;
        }
    }

    private bool ShouldForwardWake()
    {
        lock (_modeGate)
        {
            return _mode is VoiceTriggerMode.WakeWordOnly or VoiceTriggerMode.Both;
        }
    }

    public void Dispose()
    {
        _pttCapture.UtteranceReady -= OnPttUtteranceReady;
        _pttCapture.CaptureFailed -= OnPttCaptureFailed;
        _pttCapture.CaptureEmpty -= OnPttCaptureEmpty;

        _wakeCapture.UtteranceReady -= OnWakeUtteranceReady;
        _wakeCapture.CaptureFailed -= OnWakeCaptureFailed;
        _wakeCapture.CaptureEmpty -= OnWakeCaptureEmpty;
        _wakeCapture.StatusChanged -= OnWakeStatusChanged;

        _wakeCapture.Dispose();
    }
}
