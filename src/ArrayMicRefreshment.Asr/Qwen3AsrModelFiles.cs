namespace ArrayMicRefreshment.Asr;

public sealed record Qwen3AsrModelFiles(
    string ConvFrontendPath,
    string EncoderPath,
    string DecoderPath,
    string TokenizerDir);
