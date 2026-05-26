using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Audio;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class WakeWordDetectorFactory
{
    public static IWakeWordDetector Create(AppSettings settings)
    {
        var modelsDir = string.IsNullOrWhiteSpace(settings.ModelsDirectory)
            ? "models"
            : settings.ModelsDirectory;
        var phrase = settings.WakeWordPhrase;

        if (SherpaKeywordWakeWordDetector.TryCreate(modelsDir, phrase, out var sherpa) && sherpa is not null)
        {
            Log.Information(
                "Wake-word detector: Sherpa KWS (phrase={Phrase}, models={ModelsDir})",
                phrase,
                ModelsPathResolver.Resolve(modelsDir));
            return sherpa;
        }

        var resolved = ModelsPathResolver.Resolve(modelsDir);
        Log.Warning(
            "Sherpa KWS model not found under {ModelsDir}\\{ModelDir}. " +
            "Wake phrase in settings will NOT be detected from audio until the model is installed. " +
            "Run: .\\scripts\\download-models.ps1 -IncludeKws",
            resolved,
            WakeWordModelPaths.ModelDirName);
        return new StubWakeWordDetector(phrase);
    }
}
