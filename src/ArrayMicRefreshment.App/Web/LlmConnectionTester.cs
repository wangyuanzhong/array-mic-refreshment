using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Prompt;

namespace ArrayMicRefreshment.App.Web;

/// <summary>LLM connectivity test (mirrors <see cref="SettingsForm"/> test connection).</summary>
public static class LlmConnectionTester
{
    public static async Task<LlmTestResultDto> TestAsync(
        AppSettings settings,
        HttpMessageHandler? handler = null,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var draft = CloneForTest(settings);
            draft.PromptRefineEnabled = true;
            draft.ApiBaseUrl = ApiUrlNormalizer.NormalizeBaseUrl(draft.ApiBaseUrl);

            if (string.IsNullOrWhiteSpace(draft.ApiBaseUrl))
            {
                return Fail(sw, "失败：请先填写 API Base URL");
            }

            var catalog = SkillsCatalog.Load(SkillsPathResolver.Resolve(draft.SkillsDirectory));
            if (catalog.MissingFiles.Count > 0)
            {
                return Fail(sw, $"失败：缺少文件 {string.Join(", ", catalog.MissingFiles)}");
            }

            var router = new OpenAiCompatibleIntentRouter(draft, catalog, handler);
            var refiner = new OpenAiCompatiblePromptRefiner(draft, catalog, handler);
            const string sample = "ping connectivity test";

            var (_, confidence) = await router.RouteAsync(sample, cancellationToken).ConfigureAwait(false);
            _ = await refiner.RefineAsync(sample, PromptIntent.GeneralAi, cancellationToken).ConfigureAwait(false);

            sw.Stop();
            return new LlmTestResultDto
            {
                Ok = true,
                ElapsedMs = sw.ElapsedMilliseconds,
                RouterConfidence = confidence,
                Message = $"成功（{sw.ElapsedMilliseconds} ms，router confidence={confidence:F2}）",
            };
        }
        catch (RefineApiException ex)
        {
            sw.Stop();
            return Fail(sw, $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Fail(sw, $"失败（{sw.ElapsedMilliseconds} ms）: {ex.Message}");
        }
    }

    private static LlmTestResultDto Fail(System.Diagnostics.Stopwatch sw, string message) =>
        new()
        {
            Ok = false,
            ElapsedMs = sw.ElapsedMilliseconds,
            Message = message,
        };

    private static AppSettings CloneForTest(AppSettings source)
    {
        var clone = new AppSettings();
        SettingsCopier.CopyInto(source, clone);
        return clone;
    }
}
