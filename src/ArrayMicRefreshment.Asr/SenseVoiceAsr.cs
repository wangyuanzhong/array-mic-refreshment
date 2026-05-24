using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public sealed class SenseVoiceAsr : IUtteranceAsr, IDisposable
{
    private readonly IOfflineSenseVoiceBackend _backend;

    public string ModelId { get; }

    public SenseVoiceAsr(IOfflineSenseVoiceBackend backend, string modelId)
    {
        _backend = backend;
        ModelId = modelId;
    }

    public static SenseVoiceAsr CreateFromSettings(AppSettings settings)
    {
        var paths = SenseVoiceModelResolver.Resolve(settings.ModelsDirectory, settings.SelectedAsrModelId);
        Log.Information(
            "SenseVoice ASR loaded: {ModelId} from {Directory} (ONNX: {ModelPath})",
            paths.ModelId,
            paths.DirectoryPath,
            paths.ModelPath);
        return new SenseVoiceAsr(new SherpaSenseVoiceBackend(paths), paths.ModelId);
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

        var text = SenseVoiceTextExtractor.ExtractPlainText(raw);
        var cleaned = SpeechCleaner.Clean(text);
        if (cleaned != text)
        {
            Log.Debug("SpeechCleaner removed {Removed} filler chars", text.Length - cleaned.Length);
        }
        Log.Debug("SenseVoice recognized {Chars} characters", cleaned.Length);
        return cleaned;
    }

    public void Dispose() => _backend.Dispose();
}
