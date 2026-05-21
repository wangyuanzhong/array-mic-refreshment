namespace ArrayMicRefreshment.Core;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
