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

        var resolved = ModelsPathResolver.Resolve(modelsDir);
        var filesPresent = WakeWordModelPaths.TryResolve(modelsDir, out _);

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
                resolved);
            Log.Information(
                "Wake-word detector: Sherpa KWS (phrase={Phrase}, models={ModelsDir})",
                phrase,
                resolved);
            return sherpa;
        }

        if (filesPresent)
        {
            Log.Warning(
                "[WAKE-DIAG] wake detector=stub phrase={Phrase} — KWS files exist under {ModelsDir} but Sherpa engine failed to load (see earlier exception).",
                phrase,
                resolved);
            Log.Warning(
                "Sherpa KWS files are present under {ModelsDir}\\{ModelDir} but the keyword spotter could not start. " +
                "Check app log for load errors; settings may show「已安装」while wake detection stays disabled.",
                resolved,
                WakeWordModelPaths.ModelDirName);
        }
        else
        {
            Log.Warning(
                "[WAKE-DIAG] wake detector=stub phrase={Phrase} modelsDir={ModelsDir}. " +
                "Install KWS with: .\\scripts\\download-models.ps1 -IncludeKws",
                phrase,
                resolved);
            Log.Warning(
                "Sherpa KWS model not found under {ModelsDir}\\{ModelDir}. " +
                "Wake phrase in settings will NOT be detected from audio until the model is installed. " +
                "Run: .\\scripts\\download-models.ps1 -IncludeKws",
                resolved,
                WakeWordModelPaths.ModelDirName);
        }

        return new StubWakeWordDetector(phrase);
    }
}
