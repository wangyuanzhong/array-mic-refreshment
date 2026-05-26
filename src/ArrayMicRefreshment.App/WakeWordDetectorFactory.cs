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

        if (SherpaKeywordWakeWordDetector.TryCreate(
                modelsDir,
                phrase,
                out var sherpa,
                settings.WakeWordSensitivity)
            && sherpa is not null)
        {
            Log.Information(
                "[WAKE-DIAG] wake detector=sherpa-kws phrase={Phrase} sensitivity={Sensitivity} models={ModelsDir}",
                phrase,
                settings.WakeWordSensitivity,
                ModelsPathResolver.Resolve(modelsDir));
            Log.Information(
                "Wake-word detector: Sherpa KWS (phrase={Phrase}, models={ModelsDir})",
                phrase,
                ModelsPathResolver.Resolve(modelsDir));
            return sherpa;
        }

        var resolved = ModelsPathResolver.Resolve(modelsDir);
        Log.Warning(
            "[WAKE-DIAG] wake detector=stub phrase={Phrase} sensitivity={Sensitivity} modelsDir={ModelsDir}. " +
            "Real wake-word detection disabled until sherpa-kws model is installed.",
            phrase,
            settings.WakeWordSensitivity,
            resolved);
        Log.Warning(
            "Sherpa KWS model not found under {ModelsDir}\\{ModelDir}. " +
            "Wake phrase in settings will NOT be detected from audio until the model is installed. " +
            "Run: .\\scripts\\download-models.ps1 -IncludeKws",
            resolved,
            WakeWordModelPaths.ModelDirName);
        return new StubWakeWordDetector(phrase);
    }
}
