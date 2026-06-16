using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public sealed class SenseVoiceAsr : IUtteranceAsr, IDisposable
{
    private readonly IOfflineAsrBackend _backend;
    private readonly AsrEngineKind _engine;

    public string ModelId { get; }

    public SenseVoiceAsr(IOfflineAsrBackend backend, string modelId, AsrEngineKind engine = AsrEngineKind.SenseVoice)
    {
        _backend = backend;
        ModelId = modelId;
        _engine = engine;
    }

    public static SenseVoiceAsr CreateFromSettings(AppSettings settings)
    {
        var paths = AsrModelResolver.Resolve(settings.ModelsDirectory, settings.SelectedAsrModelId);
        var threads = AsrInferenceTuning.ResolveNumThreads();
        Log.Information(
            "Sherpa ASR loaded: {Engine} {ModelId} from {Directory} (threads={Threads}, ONNX: {ModelPath})",
            paths.Engine,
            paths.ModelId,
            paths.DirectoryPath,
            threads,
            paths.Engine == AsrEngineKind.Qwen3Asr
                ? paths.Qwen3?.EncoderPath
                : paths.ModelPath);
        return new SenseVoiceAsr(AsrBackendFactory.Create(paths, threads), paths.ModelId, paths.Engine);
    }

    public void Warmup()
    {
        try
        {
            _backend.Warmup();
            Log.Debug("ASR warmup completed for {ModelId} ({Engine})", ModelId, _engine);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ASR warmup failed for {ModelId}", ModelId);
        }
    }

    public async Task<string> RecognizeUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken)
    {
        var floats = PcmConverters.Ensure16KHzMonoFloats(utterance.Pcm16LeMono, utterance.SampleRate);

        cancellationToken.ThrowIfCancellationRequested();

        var raw = await Task.Run(
                () => _backend.Decode(floats.AsMemory(), PcmConverters.TargetSampleRate),
                cancellationToken)
            .ConfigureAwait(false);

        var text = _engine switch
        {
            AsrEngineKind.SenseVoice => SenseVoiceTextExtractor.ExtractPlainText(raw),
            AsrEngineKind.Qwen3Asr => raw.Trim(),
            _ => raw.Trim(),
        };
        var cleaned = SpeechCleaner.Clean(text);
        if (cleaned != text)
        {
            Log.Debug("SpeechCleaner removed {Removed} filler chars", text.Length - cleaned.Length);
        }

        Log.Debug("ASR ({Engine}) recognized {Chars} characters", _engine, cleaned.Length);
        return cleaned;
    }

    public void Dispose() => _backend.Dispose();
}
