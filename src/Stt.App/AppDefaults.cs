using Stt.Core.Models;

namespace Stt.App;

public static class AppDefaults
{
    public const string TranscriptionModel = "gpt-4o-mini-transcribe";
    public const RealtimeVadMode DefaultRealtimeVadMode = RealtimeVadMode.SemanticVad;
    public const int DefaultRealtimeSilenceDurationMs = 900;
    public const int DefaultRealtimePrefixPaddingMs = 300;
    public const RealtimeVadEagerness DefaultRealtimeVadEagerness = RealtimeVadEagerness.Low;
}
