using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;
using Serilog;

namespace ArrayMicRefreshment.Asr;

public sealed class SenseVoiceAsr : IUtteranceAsr, IDisposable
{
    private readonly IOfflineSenseVoiceBackend _backend;

    public SenseVoiceAsr(IOfflineSenseVoiceBackend backend)
    {
        _backend = backend;
    }

    public static SenseVoiceAsr CreateFromSettings(AppSettings settings)
    {
        var paths = SenseVoiceModelResolver.Resolve(settings.ModelsDirectory);
        return new SenseVoiceAsr(new SherpaSenseVoiceBackend(paths));
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
        Log.Debug("SenseVoice recognized {Chars} characters", text.Length);
        return text;
    }

    public void Dispose() => _backend.Dispose();
}
