using Serilog;

namespace ArrayMicRefreshment.Core;

public sealed class VoicePipeline
{
    private const double MinRmsForSpeech = 0.003;

    /// <summary>~0.1 s at 16 kHz mono 16-bit.</summary>
    private const int MinPcmBytes = 3200;

    private AppSettings _settings;
    private readonly ISpeakerGate _speakerGate;
    private readonly IUtteranceAsr _asr;
    private readonly IIntentRouter _router;
    private readonly IPromptRefiner _promptRefiner;
    private readonly ITranscriptSink _sink;

    public VoicePipeline(
        AppSettings settings,
        ISpeakerGate speakerGate,
        IUtteranceAsr asr,
        IIntentRouter router,
        IPromptRefiner promptRefiner,
        ITranscriptSink sink)
    {
        _settings = settings;
        _speakerGate = speakerGate;
        _asr = asr;
        _router = router;
        _promptRefiner = promptRefiner;
        _sink = sink;
    }

    public void ApplySettings(AppSettings settings)
    {
        Log.Information(
            "[DIAGNOSTIC] VoicePipeline.ApplySettings called. " +
            "Old PromptRefineEnabled={OldRefine}, New PromptRefineEnabled={NewRefine}, " +
            "RefinerType={RefinerType}, RefinerCurrentEnabled={RefinerEnabled}",
            _settings.PromptRefineEnabled,
            settings.PromptRefineEnabled,
            _promptRefiner.GetType().Name,
            _promptRefiner.IsEnabled);

        _settings = settings;
        _router.ApplySettings(settings);
        _promptRefiner.ApplySettings(settings);

        Log.Information(
            "[DIAGNOSTIC] VoicePipeline.ApplySettings completed. " +
            "RefinerNewEnabled={RefinerEnabled}",
            _promptRefiner.IsEnabled);
    }

    public async Task<VoicePipelineOutcome> ProcessUtteranceAsync(
        AudioUtterance utterance,
        CancellationToken cancellationToken,
        VoiceTriggerKind triggerKind = VoiceTriggerKind.Ptt)
    {
        if (!_settings.MasterEnabled)
        {
            return new VoicePipelineOutcome(VoicePipelineStatus.SkippedMasterDisabled);
        }

        if (utterance.Pcm16LeMono.Length < MinPcmBytes)
        {
            var shortHint = triggerKind == VoiceTriggerKind.WakeWord
                ? "唤醒后请稍大声、清晰地说出指令（至少约 0.2 秒）。"
                : "请完整按住 PTT 组合键至少 0.5 秒再松开；若仍如此，在设置中换 [MME] 录音设备。";
            return new VoicePipelineOutcome(
                VoicePipelineStatus.EmptyTranscript,
                $"录音过短（{utterance.Duration.TotalMilliseconds:F0} ms，{utterance.Pcm16LeMono.Length} 字节）。{shortHint}");
        }

        var rms = Core.Audio.PcmConverters.ComputeRms16Le(utterance.Pcm16LeMono);
        if (rms < MinRmsForSpeech)
        {
            return new VoicePipelineOutcome(
                VoicePipelineStatus.EmptyTranscript,
                $"未检测到有效麦克风信号（音量过低 RMS={rms:F4}）。请检查录音设备、系统麦克风权限与音量。");
        }

        // Wake-word and PTT both require speaker verification after capture.
        // WakeWordCaptureService may supply SpeakerVerifyPcm16LeMono (post-wake command only).
        var speakerTask = _speakerGate.VerifyCurrentUserAsync(utterance, cancellationToken);

        var asrTask = _asr.RecognizeUtteranceAsync(utterance, cancellationToken);
        await Task.WhenAll(speakerTask, asrTask).ConfigureAwait(false);

        var speakerVerify = speakerTask.Result;
        if (!speakerVerify.Allowed && !speakerVerify.VerificationSkipped)
        {
            var effectiveThreshold = speakerVerify.EffectiveThreshold > 0f
                ? speakerVerify.EffectiveThreshold
                : _settings.SpeakerVerifyThreshold;
            var windowHint = speakerVerify.WindowAverage > 0f
                ? $"，近几次均值 {speakerVerify.WindowAverage:F2}"
                : string.Empty;
            return new VoicePipelineOutcome(
                VoicePipelineStatus.SpeakerRejected,
                $"说话人未通过声纹校验（相似度 {speakerVerify.Score:F2}，有效阈值 {effectiveThreshold:F2}{windowHint}）。" +
                "请重新「注册说话人」或略降低声纹阈值；旧版注册数据已自动归一化，若仍失败请重录。");
        }

        var raw = asrTask.Result;
        if (triggerKind == VoiceTriggerKind.WakeWord)
        {
            raw = StripLeadingWakePhrase(raw, _settings.WakeWordPhrase);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new VoicePipelineOutcome(
                VoicePipelineStatus.EmptyTranscript,
                $"ASR 未返回文字（录音 {utterance.Duration.TotalSeconds:F1}s）。请换 [MME] 设备或确认 models 文件夹在 exe 旁。");
        }

        Log.Information("ASR raw transcript: {Raw}", raw);

        var output = raw;
        string? refineSkipReason = null;

        // DIAGNOSTIC: log the exact values that determine whether refine runs
        Log.Information(
            "[DIAGNOSTIC] Refine gate: settings.PromptRefineEnabled={SettingsEnabled}, " +
            "refiner.Type={RefinerType}, refiner.IsEnabled={RefinerEnabled}",
            _settings.PromptRefineEnabled,
            _promptRefiner.GetType().Name,
            _promptRefiner.IsEnabled);

        if (_settings.PromptRefineEnabled && _promptRefiner.IsEnabled)
        {
            Log.Information("Prompt refine enabled. Starting refine pipeline...");

            try
            {
                PromptIntent intent;
                if (_settings.ForcedIntent == PromptIntent.Auto)
                {
                    intent = (await _router.RouteAsync(raw, cancellationToken).ConfigureAwait(false)).Intent;
                    Log.Information("Router resolved intent: {Intent}", intent);
                }
                else if (_settings.ForcedIntent == PromptIntent.PlainText)
                {
                    intent = PromptIntent.PlainText;
                    Log.Information("Using selected skill: PlainText (built-in transcript polish)");
                }
                else
                {
                    intent = _settings.ForcedIntent;
                    Log.Information("Using selected skill: {Intent}", intent);
                }

                var refined = await _promptRefiner.RefineAsync(raw, intent, cancellationToken).ConfigureAwait(false);
                Log.Information("Prompt refined completed. Input={RawLen} chars, Output={RefinedLen} chars", raw.Length, refined?.Length ?? 0);

                // Failure only when the LLM returned no usable text (not when output equals raw).
                if (string.IsNullOrWhiteSpace(refined))
                {
                    Log.Warning("Prompt refine returned empty/whitespace. Falling back to raw text.");
                    output = raw;
                    await _sink.EmitAsync(output, _settings.PasteToCaretEnabled, cancellationToken).ConfigureAwait(false);
                    return new VoicePipelineOutcome(
                        VoicePipelineStatus.EmittedRawFallback,
                        $"[整理返回空，使用原文] {output}",
                        _asr.ModelId,
                        false,
                        "返回空内容");
                }

                output = refined;
                var unchanged = string.Equals(output, raw, StringComparison.Ordinal);
                if (unchanged)
                {
                    Log.Debug("Prompt refine succeeded: LLM returned non-empty text identical to ASR raw.");
                }

                await _sink.EmitAsync(output, _settings.PasteToCaretEnabled, cancellationToken).ConfigureAwait(false);
                return new VoicePipelineOutcome(
                    VoicePipelineStatus.Emitted,
                    output,
                    _asr.ModelId,
                    RefineApplied: true,
                    RefineStatus: "成功");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Prompt refine failed. Raw text will be used. OnRefineFailure={Mode}", _settings.OnRefineFailure);

                if (_settings.OnRefineFailure == OnRefineFailure.UseRawTranscript)
                {
                    output = raw;
                    Log.Information("Fallback to raw text: '{Output}'", output);
                    await _sink.EmitAsync(output, _settings.PasteToCaretEnabled, cancellationToken).ConfigureAwait(false);
                    Log.Information("EmitAsync completed");
                    var errorDetail = $"{ex.GetType().Name}: {ex.Message}";
                    return new VoicePipelineOutcome(
                        VoicePipelineStatus.EmittedRawFallback,
                        $"[整理失败，使用原文] {output}",
                        _asr.ModelId,
                        RefineApplied: false,
                        RefineStatus: $"失败: {errorDetail}");
                }

                throw;
            }
        }
        else
        {
            var reason = !_settings.PromptRefineEnabled
                ? "设置中未启用「提示词整理」"
                : "整理器未就绪";
            Log.Information(
                "Prompt refine skipped ({Reason}). Enabled={Enabled}, RefinerEnabled={RefinerEnabled}",
                reason,
                _settings.PromptRefineEnabled,
                _promptRefiner.IsEnabled);
            refineSkipReason = reason;
        }

        await _sink.EmitAsync(output, _settings.PasteToCaretEnabled, cancellationToken).ConfigureAwait(false);

        return new VoicePipelineOutcome(
            VoicePipelineStatus.Emitted,
            output,
            _asr.ModelId,
            RefineApplied: false,
            RefineStatus: refineSkipReason ?? "未启用");
    }

    private static string StripLeadingWakePhrase(string raw, string wakePhrase)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(wakePhrase))
        {
            return raw;
        }

        var text = raw.Trim();
        var phrase = wakePhrase.Trim();
        if (text.StartsWith(phrase, StringComparison.Ordinal))
        {
            return text[phrase.Length..].TrimStart(' ', '，', ',', '。', '.', '、');
        }

        return raw;
    }
}
