using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class SherpaPipelineFactory
{
    public sealed record PipelineComponents(IUtteranceAsr Asr, ISpeakerGate Speaker, bool ModelsMissing)
    {
        public void DisposeOwned()
        {
            if (Asr is IDisposable asrDisposable)
            {
                asrDisposable.Dispose();
            }

            if (Speaker is IDisposable speakerDisposable)
            {
                speakerDisposable.Dispose();
            }
        }
    }

    public static PipelineComponents CreateOrFallback(AppSettings settings, ISettingsStore settingsStore)
    {
        try
        {
            var asr = SenseVoiceAsr.CreateFromSettings(settings);
            var speaker = SpeakerGate.CreateFromSettings(settings, settingsStore);
            Log.Information("Sherpa SenseVoice ASR and speaker gate loaded.");
            return new PipelineComponents(asr, speaker, ModelsMissing: false);
        }
        catch (ModelNotFoundException ex)
        {
            Log.Warning(ex, "Sherpa models missing; using pipeline stubs.");
            return CreateStubs(modelsMissing: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Sherpa pipeline; using stubs.");
            return CreateStubs(modelsMissing: true);
        }
    }

    private static PipelineComponents CreateStubs(bool modelsMissing) =>
        new(new StubUtteranceAsr(), new StubSpeakerGate { AlwaysPass = true }, modelsMissing);
}
