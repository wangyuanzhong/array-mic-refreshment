using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class SherpaPipelineFactory
{
    public sealed record PipelineComponents(
        IUtteranceAsr Asr,
        ISpeakerGate Speaker,
        bool AsrModelsMissing,
        bool SpeakerModelMissing)
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
        IUtteranceAsr asr;
        var asrMissing = false;
        try
        {
            asr = SenseVoiceAsr.CreateFromSettings(settings);
            Log.Information("Sherpa SenseVoice ASR loaded.");
        }
        catch (ModelNotFoundException ex)
        {
            Log.Warning(ex, "SenseVoice ASR model missing; using stub.");
            asr = new StubUtteranceAsr();
            asrMissing = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize SenseVoice ASR; using stub.");
            asr = new StubUtteranceAsr();
            asrMissing = true;
        }

        ISpeakerGate speaker;
        var speakerMissing = false;
        try
        {
            speaker = SpeakerGate.CreateFromSettings(settings, settingsStore);
            Log.Information("Sherpa speaker gate loaded.");
        }
        catch (ModelNotFoundException ex)
        {
            Log.Warning(ex, "Speaker embedding model missing; verification disabled.");
            speaker = new UnavailableSpeakerGate();
            speakerMissing = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize speaker gate; verification disabled.");
            speaker = new UnavailableSpeakerGate();
            speakerMissing = true;
        }

        return new PipelineComponents(asr, speaker, asrMissing, speakerMissing);
    }
}
