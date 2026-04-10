using Stt.Core.Models;

namespace Stt.Infrastructure.OpenAi;

public sealed record OpenAiTranscriptionOptions(
    string? ApiKey,
    string TranscriptionModel,
    string? TranscriptionLanguage = null,
    string? TranscriptionPrompt = null,
    RealtimeVadMode RealtimeVadMode = RealtimeVadMode.SemanticVad,
    int RealtimeSilenceDurationMs = 1000,
    int RealtimePrefixPaddingMs = 300,
    RealtimeVadEagerness RealtimeVadEagerness = RealtimeVadEagerness.Low);
