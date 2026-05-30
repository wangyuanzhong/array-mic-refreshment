namespace ArrayMicRefreshment.Asr;

/// <summary>Public entry for shipping default Sherpa KWS ppinyin encodings.</summary>
public static class WakeWordEncodingBootstrap
{
    public static void EnsureDefaultEncodings(string modelRoot) =>
        WakeWordBuiltinEncodings.EnsureCopiedToModelRoot(modelRoot);
}
