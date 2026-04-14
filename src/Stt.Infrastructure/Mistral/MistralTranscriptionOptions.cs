namespace Stt.Infrastructure.Mistral;

public sealed record MistralTranscriptionOptions(
    string? ApiKey,
    string TranscriptionModel,
    string? TranscriptionLanguage = null);
