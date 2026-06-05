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
        Log.Information(
            "Sherpa ASR loaded: {Engine} {ModelId} from {Directory} (ONNX: {ModelPath})",
            paths.Engine,
            paths.ModelId,
            paths.DirectoryPath,
            paths.ModelPath);
        return new SenseVoiceAsr(AsrBackendFactory.Create(paths), paths.ModelId, paths.Engine);
    }

    public async Task<string> RecognizeUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken)
    {
        var pcm = PcmConverters.Ensure16KHzMonoPcm16Le(utterance.Pcm16LeMono, utterance.SampleRate);
        var floats = PcmConverters.Pcm16LeToFloat(pcm);

        cancellationToken.ThrowIfCancellationRequested();

        var raw = await Task.Run(
                () => _backend.Decode(floats, PcmConverters.TargetSampleRate),
                cancellationToken)
            .ConfigureAwait(false);

        var text = _engine switch
        {
            AsrEngineKind.SenseVoice => SenseVoiceTextExtractor.ExtractPlainText(raw),
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
