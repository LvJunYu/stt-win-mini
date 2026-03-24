namespace Stt.App;

public static class AppIdentity
{
    public const string DisplayName = "whisper";
    public const string SingleInstanceMutexName = @"Local\whisper.SingleInstance";
    public const string SettingsDirectoryName = "whisper";
    public const string SettingsFileName = "whisper.settings.json";
    public const string StartupValueName = "whisper";

    public const string LegacySettingsDirectoryName = "JotMic";
    public const string LegacySettingsFileName = "jotmic.settings.json";
    public const string LegacyStartupValueName = "JotMic";

    public const string OlderLegacySettingsDirectoryName = "Stt";
    public const string OlderLegacySettingsFileName = "stt.settings.json";
    public const string OlderLegacyStartupValueName = "Stt.App";
}
