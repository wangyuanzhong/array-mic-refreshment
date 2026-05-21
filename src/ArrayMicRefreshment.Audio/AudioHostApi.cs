namespace ArrayMicRefreshment.Audio;

/// <summary>Capture backend protocol (MME, WDM/DirectSound, WASAPI).</summary>
public enum AudioHostApi
{
    Wasapi,
    WdmDirectSound,
    Mme,
}
