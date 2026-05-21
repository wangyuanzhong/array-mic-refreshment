namespace ArrayMicRefreshment.Core;

public sealed class VoicePipeline
{
    private readonly AppSettings _settings;
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

    public async Task ProcessUtteranceAsync(AudioUtterance utterance, CancellationToken cancellationToken)
    {
        if (!_settings.MasterEnabled)
        {
            return;
        }

        if (!await _speakerGate.VerifyCurrentUserAsync(utterance, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var raw = await _asr.RecognizeUtteranceAsync(utterance, cancellationToken).ConfigureAwait(false);
        var output = raw;

        if (_settings.PromptRefineEnabled && _promptRefiner.IsEnabled)
        {
            try
            {
                var intent = _settings.ForcedIntent == PromptIntent.Auto
                    ? (await _router.RouteAsync(raw, cancellationToken).ConfigureAwait(false)).Intent
                    : _settings.ForcedIntent;
                output = await _promptRefiner.RefineAsync(raw, intent, cancellationToken).ConfigureAwait(false);
            }
            catch when (_settings.OnRefineFailure == OnRefineFailure.UseRawTranscript)
            {
                output = raw;
            }
        }

        await _sink.EmitAsync(output, _settings.PasteToCaretEnabled, cancellationToken).ConfigureAwait(false);
    }
}
