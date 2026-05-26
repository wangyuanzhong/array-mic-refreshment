using System.Runtime.InteropServices;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// Wake-word path: one mic stream, inline KWS, dictation ends on adaptive silence (not a hard speech cap).
/// </summary>
public sealed class WakeWordCaptureService : IWakeWordCaptureService
{
    public static readonly TimeSpan DefaultPostWakeSilenceTimeout = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan DefaultPostWakeStartGrace = TimeSpan.FromSeconds(4);
    /// <summary>Safety net only — normal sessions end on silence, not on this limit.</summary>
    public static readonly TimeSpan DefaultPostWakeMaxSession = TimeSpan.FromMinutes(5);
    /// <summary>Force-submit command audio if user keeps talking/noise persists.</summary>
    public static readonly TimeSpan DefaultPostWakeMaxCommand = TimeSpan.FromSeconds(12);

    private const int SessionPollMs = 25;
    private const int PreRollBytes = 64_000;
    private const double MinSpeechRms = 0.0025;
    private const double MinCommandStartRms = 0.008;
    private const double MinContinueSpeechRms = 0.012;
    private const double NoiseFloorMultiplier = 4.0;
    private const double ContinueThresholdMultiplier = 2.5;
    /// <summary>Extend end-of-speech timer only when chunk energy is a meaningful fraction of session peak.</summary>
    private const double PeakContinueFraction = 0.35;
    /// <summary>Ignore wake-phrase tail / room echo before accepting command speech.</summary>
    public static readonly TimeSpan DefaultPostWakeEchoIgnore = TimeSpan.FromMilliseconds(450);
    private const int MinSpeechChunksBeforeAccept = 3;

    private readonly AppSettings _settings;
    private readonly IWakeWordDetector _detector;
    private readonly IAudioDeviceEnumerator _deviceEnumerator;
    private readonly IAudioCaptureStreamFactory _captureFactory;
    private readonly TimeSpan _silenceTimeout;
    private readonly TimeSpan _startGrace;
    private readonly TimeSpan _maxSession;
    private readonly TimeSpan _maxCommand;
    private readonly TimeSpan _echoIgnore;
    private readonly ByteRingBuffer _preRoll = new(PreRollBytes);
    private readonly object _gate = new();

    private IAudioCaptureStream? _stream;
    private string? _activeDeviceId;
    private bool _listening;
    private bool _dictationActive;
    private readonly List<byte> _dictationBuffer = new();
    private bool _heardPostWakeSpeech;
    private DateTimeOffset _dictationStartedUtc;
    private DateTimeOffset _lastSpeechUtc;
    private double _noiseFloorRms = 0.001;
    private int _speechStartByteIndex = -1;
    private int _speechEndByteIndex;
    private int _consecutiveSpeechChunks;
    private int _speechCandidateStartIndex = -1;
    private double _speechCandidatePeakRms;
    private double _sessionPeakRms;
    private bool _commandWindowOpen;
    private int _commandBaseIndex;
    private DateTimeOffset? _commandSpeechStartedUtc;
    private System.Threading.Timer? _sessionTimer;
    private WakeWordDetectedEventArgs? _pendingWakeActivated;

    public WakeWordCaptureService(
        AppSettings settings,
        IWakeWordDetector detector,
        IAudioDeviceEnumerator deviceEnumerator,
        IAudioCaptureStreamFactory captureFactory,
        TimeSpan? postWakeSilenceTimeout = null,
        TimeSpan? postWakeStartGrace = null,
        TimeSpan? postWakeMaxSession = null,
        TimeSpan? postWakeMaxCommand = null,
        TimeSpan? postWakeEchoIgnore = null)
    {
        _settings = settings;
        _detector = detector;
        _deviceEnumerator = deviceEnumerator;
        _captureFactory = captureFactory;
        _silenceTimeout = postWakeSilenceTimeout ?? DefaultPostWakeSilenceTimeout;
        _startGrace = postWakeStartGrace ?? DefaultPostWakeStartGrace;
        _maxSession = postWakeMaxSession ?? DefaultPostWakeMaxSession;
        _maxCommand = postWakeMaxCommand ?? DefaultPostWakeMaxCommand;
        _echoIgnore = postWakeEchoIgnore ?? DefaultPostWakeEchoIgnore;
        _detector.WakeWordDetected += OnWakeWordDetected;
    }

    public event EventHandler<UtteranceCaptureEventArgs>? UtteranceReady;
    public event EventHandler<Exception>? CaptureFailed;
    public event EventHandler<string>? CaptureEmpty;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordActivated;

    public bool IsListening
    {
        get { lock (_gate) { return _listening; } }
    }

    public bool IsDictationActive
    {
        get { lock (_gate) { return _dictationActive; } }
    }

    public void StartListening()
    {
        lock (_gate)
        {
            if (_listening)
            {
                return;
            }

            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
                    ?? throw new InvalidOperationException("No capture device available.");

                _preRoll.Clear();
                _totalListenBytes = 0;
                EnsureCaptureStream(device);
                _stream!.Start();
                _detector.Start();
                _listening = true;
                RaiseStatus("监听唤醒词…");
                Log.Information(
                    "Wake-word listening started (detector={DetectorId}, device={Device})",
                    _detector.DetectorId,
                    device.DisplayName);
            }
            catch (Exception ex)
            {
                StopListeningInternal(finalizeDictation: false);
                CaptureFailed?.Invoke(this, ex);
                Log.Warning(ex, "Failed to start wake-word listening");
            }
        }
    }

    public void StopListening()
    {
        lock (_gate)
        {
            StopListeningInternal(finalizeDictation: false);
        }
    }

    /// <summary>Re-open mic + reset KWS without leaving listen mode (settings/device refresh).</summary>
    public void RestartListening()
    {
        lock (_gate)
        {
            if (!_listening)
            {
                return;
            }

            if (_dictationActive)
            {
                if (_heardPostWakeSpeech)
                {
                    EndDictationSessionInternal();
                }
                else
                {
                    CancelDictationSession();
                }
            }

            _detector.Stop();
            StopCaptureStream();
            _preRoll.Clear();
            _totalListenBytes = 0;

            try
            {
                var device = _deviceEnumerator.ResolveDevice(_settings.SelectedDeviceId)
                    ?? throw new InvalidOperationException("No capture device available.");
                EnsureCaptureStream(device);
                _stream!.Start();
                _detector.Start();
                RaiseStatus("监听唤醒词…");
                Log.Information("Wake-word listening restarted (device={Device})", device.DisplayName);
            }
            catch (Exception ex)
            {
                _listening = false;
                CaptureFailed?.Invoke(this, ex);
                Log.Warning(ex, "Failed to restart wake-word listening");
            }
        }
    }

    private void StopListeningInternal(bool finalizeDictation)
    {
        _detector.Stop();
        if (_dictationActive)
        {
            if (finalizeDictation || _heardPostWakeSpeech)
            {
                // Full stop — do not resume KWS inside EndDictationSessionInternal.
                _listening = false;
                EndDictationSessionInternal();
            }
            else
            {
                CancelDictationSession();
            }
        }

        StopCaptureStream();
        _listening = false;
        _preRoll.Clear();
        _totalListenBytes = 0;
        RaiseStatus("唤醒监听已停止");
        Log.Information("Wake-word listening stopped");
    }

    private void OnWakeWordDetected(object? sender, WakeWordDetectedEventArgs e)
    {
        bool shouldBeginDictation;
        lock (_gate)
        {
            if (!_listening || _dictationActive)
            {
                return;
            }

            Log.Information("Wake word detected: {Keyword} at {Utc}", e.Keyword, e.DetectedAtUtc);
            _pendingWakeActivated = e;
            shouldBeginDictation = true;
        }

        if (shouldBeginDictation && _pendingWakeActivated is not null)
        {
            var activated = _pendingWakeActivated;
            _pendingWakeActivated = null;
            WakeWordActivated?.Invoke(this, activated);

            try
            {
                BeginDictationSession();
            }
            catch (Exception ex)
            {
                CaptureFailed?.Invoke(this, ex);
                Log.Warning(ex, "Failed to start post-wake dictation");
            }
        }
    }

    private void BeginDictationSession()
    {
        lock (_gate)
        {
            _detector.Stop();
            _dictationBuffer.Clear();
            _heardPostWakeSpeech = false;
            _speechStartByteIndex = -1;
            _speechEndByteIndex = 0;
            _consecutiveSpeechChunks = 0;
            _speechCandidateStartIndex = -1;
            _speechCandidatePeakRms = 0;
            _sessionPeakRms = 0;
            _noiseFloorRms = 0.001;
            _commandWindowOpen = false;
            _commandBaseIndex = 0;
            _commandSpeechStartedUtc = null;
            _preRoll.Clear();
            _dictationStartedUtc = DateTimeOffset.UtcNow;
            _lastSpeechUtc = _dictationStartedUtc;
            _dictationActive = true;
        }

        _sessionTimer?.Dispose();
        _sessionTimer = new System.Threading.Timer(
            _ => PollDictationSession(),
            null,
            SessionPollMs,
            SessionPollMs);

        RaiseStatus("识别中…");
        Log.Information(
            "Post-wake dictation started (silenceTimeout={SilenceMs}ms, echoIgnore={EchoMs}ms)",
            (int)_silenceTimeout.TotalMilliseconds,
            (int)_echoIgnore.TotalMilliseconds);
    }

    private void PollDictationSession()
    {
        lock (_gate)
        {
            if (!_dictationActive || _stream is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _dictationStartedUtc >= _maxSession)
            {
                Log.Information("Wake dictation ended: safety max session {Max}s reached", _maxSession.TotalSeconds);
                EndDictationSessionInternal();
                return;
            }

            if (!_heardPostWakeSpeech && now - _dictationStartedUtc >= _startGrace)
            {
                Log.Information(
                    "Wake dictation: no command speech within {Grace}s (bytes={Bytes})",
                    _startGrace.TotalSeconds,
                    _dictationBuffer.Count);
                EndDictationSessionInternal(empty: true);
                return;
            }

            if (_heardPostWakeSpeech && now - _lastSpeechUtc >= _silenceTimeout)
            {
                Log.Information(
                    "Wake dictation ended after {SilenceMs}ms silence (poll, bytes={Bytes})",
                    (int)_silenceTimeout.TotalMilliseconds,
                    _dictationBuffer.Count);
                EndDictationSessionInternal();
                return;
            }

            if (_heardPostWakeSpeech
                && _commandSpeechStartedUtc.HasValue
                && now - _commandSpeechStartedUtc.Value >= _maxCommand)
            {
                Log.Information(
                    "Wake dictation ended: max command {Max}s reached (bytes={Bytes})",
                    _maxCommand.TotalSeconds,
                    _dictationBuffer.Count);
                EndDictationSessionInternal();
            }
        }
    }

    private void EndDictationSessionInternal(bool empty = false)
    {
        if (!_dictationActive)
        {
            return;
        }

        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _dictationActive = false;

        AudioUtterance? utterance = null;
        var shouldResumeListening = _listening;
        if (!empty && _stream is not null && _dictationBuffer.Count > 0)
        {
            utterance = FinalizeUtterance();
        }

        var dictationBytes = _dictationBuffer.Count;
        _dictationBuffer.Clear();
        _speechStartByteIndex = -1;
        _speechEndByteIndex = 0;
        _consecutiveSpeechChunks = 0;
        _speechCandidateStartIndex = -1;
        _speechCandidatePeakRms = 0;
        _sessionPeakRms = 0;
        _commandWindowOpen = false;
        _commandBaseIndex = 0;
        _commandSpeechStartedUtc = null;

        // Restart detector IMMEDIATELY
        if (shouldResumeListening)
        {
            try
            {
                _preRoll.Clear();
                _detector.Start();
                RaiseStatus("监听唤醒词…");
            }
            catch (Exception ex)
            {
                _listening = false;
                CaptureFailed?.Invoke(this, ex);
            }
        }

        if (utterance is not null)
        {
            Log.Information(
                "Wake utterance finalized: {Ms:F0} ms pcm={PcmBytes} (captured={CapBytes})",
                utterance.Duration.TotalMilliseconds,
                utterance.Pcm16LeMono.Length,
                dictationBytes);
            UtteranceReady?.Invoke(
                this,
                new UtteranceCaptureEventArgs(utterance, VoiceTriggerKind.WakeWord));
            RaiseStatus("唤醒句段已提交识别");
        }
        else
        {
            CaptureEmpty?.Invoke(this, "唤醒后未录到有效语音，请重试。");
            RaiseStatus("唤醒后无有效语音");
        }
    }

    private void CancelDictationSession()
    {
        if (!_dictationActive)
        {
            return;
        }

        _sessionTimer?.Dispose();
        _sessionTimer = null;
        _dictationActive = false;
        _dictationBuffer.Clear();
        _speechStartByteIndex = -1;
        _speechEndByteIndex = 0;
        _consecutiveSpeechChunks = 0;
        _speechCandidateStartIndex = -1;
        _speechCandidatePeakRms = 0;
        _sessionPeakRms = 0;
        _commandWindowOpen = false;
        _commandBaseIndex = 0;
        _commandSpeechStartedUtc = null;
    }

    private void OpenCommandWindow()
    {
        if (_commandWindowOpen)
        {
            return;
        }

        _commandWindowOpen = true;
        _commandBaseIndex = _dictationBuffer.Count;
        _heardPostWakeSpeech = false;
        _speechStartByteIndex = -1;
        _speechEndByteIndex = 0;
        _consecutiveSpeechChunks = 0;
        _speechCandidateStartIndex = -1;
        _speechCandidatePeakRms = 0;
        _sessionPeakRms = 0;
        _commandSpeechStartedUtc = null;
        _lastSpeechUtc = DateTimeOffset.UtcNow;
        Log.Information(
            "Wake dictation: command window open (skipped {Skipped} bytes of wake echo)",
            _commandBaseIndex);
    }

    private long _totalListenBytes;
    private DateTimeOffset _lastProcessAudioLog;

    private void OnCaptureData(object? sender, ReadOnlyMemory<byte> chunk)
    {
        if (chunk.Length == 0)
        {
            return;
        }

        var endAfterSilence = false;
        short[]? samplesToProcess = null;

        lock (_gate)
        {
            if (_stream is null)
            {
                return;
            }

            if (_dictationActive)
            {
                var indexBefore = _dictationBuffer.Count;
                _dictationBuffer.AddRange(chunk.Span);
                endAfterSilence = UpdateDictationFromChunk(indexBefore, _dictationBuffer.Count, chunk);
            }
            else if (_listening)
            {
                _preRoll.Write(chunk.Span);
                _totalListenBytes += chunk.Length;

                var mono16k = PcmResampler.To16kHzMono16Le(
                    chunk.Span,
                    _stream.SampleRate,
                    _stream.Channels,
                    _stream.BitsPerSample);
                if (mono16k.Length >= 2)
                {
                    // Copy samples out of the lock — detector work must not block the gate.
                    samplesToProcess = new short[mono16k.Length / 2];
                    MemoryMarshal.Cast<byte, short>(mono16k.AsSpan()).CopyTo(samplesToProcess);
                }
            }
        }

        // Process detector OUTSIDE the gate so slow KWS inference never blocks
        // dictation polling, StopListening, or the next chunk.
        if (samplesToProcess is not null)
        {
            _detector.ProcessAudio(samplesToProcess, PcmResampler.TargetSampleRate);

            var now = DateTimeOffset.UtcNow;
            if (now - _lastProcessAudioLog >= TimeSpan.FromSeconds(5))
            {
                _lastProcessAudioLog = now;
                Log.Information(
                    "[DIAG] Wake listening active (detector={Detector}, running={Running}, bytes={Bytes})",
                    _detector.DetectorId,
                    _detector.IsRunning,
                    _totalListenBytes);
            }
        }

        if (endAfterSilence)
        {
            lock (_gate)
            {
                if (_dictationActive)
                {
                    Log.Information(
                        "Wake dictation ended after {SilenceMs}ms adaptive silence (bytes={Bytes})",
                        (int)_silenceTimeout.TotalMilliseconds,
                        _dictationBuffer.Count);
                    EndDictationSessionInternal();
                }
            }
        }
    }

    private bool UpdateDictationFromChunk(int indexBefore, int indexAfter, ReadOnlyMemory<byte> chunk)
    {
        if (_stream is null)
        {
            return false;
        }

        var mono16k = PcmResampler.To16kHzMono16Le(
            chunk.Span,
            _stream.SampleRate,
            _stream.Channels,
            _stream.BitsPerSample);
        if (mono16k.Length < 2)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _dictationStartedUtc < _echoIgnore)
        {
            return false;
        }

        if (!_commandWindowOpen)
        {
            OpenCommandWindow();
        }

        var chunkRms = ComputeRms16Le(mono16k);
        var threshold = CurrentSpeechThreshold();

        if (chunkRms < threshold)
        {
            LearnNoiseFloor(chunkRms);
            _consecutiveSpeechChunks = 0;
            _speechCandidateStartIndex = -1;
            _speechCandidatePeakRms = 0;
            return _heardPostWakeSpeech && now - _lastSpeechUtc >= _silenceTimeout;
        }

        if (_consecutiveSpeechChunks == 0)
        {
            _speechCandidateStartIndex = indexBefore;
            _speechCandidatePeakRms = chunkRms;
        }
        else
        {
            _speechCandidatePeakRms = Math.Max(_speechCandidatePeakRms, chunkRms);
        }

        _consecutiveSpeechChunks++;
        if (!_heardPostWakeSpeech
            && _consecutiveSpeechChunks >= MinSpeechChunksBeforeAccept
            && _speechCandidatePeakRms >= Math.Max(threshold, MinCommandStartRms))
        {
            _heardPostWakeSpeech = true;
            _sessionPeakRms = _speechCandidatePeakRms;
            _commandSpeechStartedUtc = now;
            _speechStartByteIndex = _speechCandidateStartIndex >= 0
                ? Math.Max(_speechCandidateStartIndex, _commandBaseIndex)
                : Math.Max(indexBefore, _commandBaseIndex);
            Log.Information(
                "Wake dictation: command speech started (peakRms={Peak:F4}, threshold={Th:F4}, bytes={Bytes})",
                _speechCandidatePeakRms,
                threshold,
                _dictationBuffer.Count);
            RaiseStatus("正在聆听指令…");
        }

        if (_heardPostWakeSpeech)
        {
            if (chunkRms >= threshold)
            {
                _speechEndByteIndex = indexAfter;
            }

            // Only strong speech resets the silence timer — ambient hum must not hold the session open.
            if (chunkRms >= ContinueSpeechThreshold())
            {
                _sessionPeakRms = Math.Max(_sessionPeakRms, chunkRms);
                _lastSpeechUtc = now;
            }
        }

        return false;
    }

    private double CurrentSpeechThreshold()
        => Math.Max(MinSpeechRms, _noiseFloorRms * NoiseFloorMultiplier);

    private double ContinueSpeechThreshold()
    {
        var floor = Math.Max(
            CurrentSpeechThreshold() * ContinueThresholdMultiplier,
            MinContinueSpeechRms);
        if (_sessionPeakRms > 0)
        {
            floor = Math.Max(floor, _sessionPeakRms * PeakContinueFraction);
        }

        return floor;
    }

    private void LearnNoiseFloor(double chunkRms)
    {
        if (chunkRms < 0.015)
        {
            _noiseFloorRms = (_noiseFloorRms * 0.98) + (chunkRms * 0.02);
        }
    }

    private AudioUtterance? FinalizeUtterance()
    {
        if (_stream is null || _dictationBuffer.Count == 0 || !_heardPostWakeSpeech)
        {
            return null;
        }

        // Same contract as PttCaptureService.FinalizeUtterance — resample only, no trim/gain/RMS gate.
        // Slice to detected speech so trailing silence (3s end delay) does not dilute pipeline RMS.
        byte[] pcm;
        if (_speechStartByteIndex >= 0 && _speechEndByteIndex > _speechStartByteIndex)
        {
            var start = Math.Max(_speechStartByteIndex, _commandBaseIndex);
            var end = Math.Min(_speechEndByteIndex, _dictationBuffer.Count);
            var length = end - start;
            if (length <= 0)
            {
                return null;
            }

            pcm = new byte[length];
            _dictationBuffer.CopyTo(start, pcm, 0, length);
            Log.Information("Wake utterance slice: start={Start} end={End} bytes={Bytes}", start, end, length);
        }
        else
        {
            pcm = _dictationBuffer.ToArray();
        }

        var resampled = PcmResampler.To16kHzMono16Le(
            pcm,
            _stream.SampleRate,
            _stream.Channels,
            _stream.BitsPerSample);
        var duration = TimeSpan.FromSeconds((double)resampled.Length / (2 * PcmResampler.TargetSampleRate));
        return new AudioUtterance
        {
            Pcm16LeMono = resampled,
            SampleRate = PcmResampler.TargetSampleRate,
            Duration = duration,
        };
    }

    private void EnsureCaptureStream(AudioDeviceInfo device)
    {
        if (_stream is not null && _activeDeviceId == device.Id)
        {
            return;
        }

        StopCaptureStream();
        _activeDeviceId = device.Id;
        _stream = _captureFactory.Open(device);
        _stream.DataAvailable += OnCaptureData;
    }

    private void StopCaptureStream()
    {
        if (_stream is null)
        {
            return;
        }

        try
        {
            _stream.Stop();
        }
        catch
        {
            // ignore stop races
        }

        _stream.DataAvailable -= OnCaptureData;
        _stream.Dispose();
        _stream = null;
    }

    private void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);

    private static double ComputeRms16Le(byte[] pcm)
    {
        if (pcm.Length < 2)
        {
            return 0;
        }

        var samples = MemoryMarshal.Cast<byte, short>(pcm.AsSpan());
        double sum = 0;
        foreach (var s in samples)
        {
            var n = s / 32768.0;
            sum += n * n;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    public void Dispose()
    {
        _detector.WakeWordDetected -= OnWakeWordDetected;
        lock (_gate)
        {
            StopListeningInternal(finalizeDictation: false);
        }

        _detector.Dispose();
    }
}
