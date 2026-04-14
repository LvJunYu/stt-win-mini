using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stt.Core.Abstractions;
using Stt.Core.Models;

namespace Stt.Infrastructure.Mistral;

public sealed class MistralTranscriptionClient : ITranscriptionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Func<MistralTranscriptionOptions> _optionsAccessor;

    public MistralTranscriptionClient(
        HttpClient httpClient,
        Func<MistralTranscriptionOptions> optionsAccessor)
    {
        _httpClient = httpClient;
        _optionsAccessor = optionsAccessor;
    }

    public void ValidateConfiguration()
    {
        var options = _optionsAccessor();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Set a Mistral API key in Settings before recording.");
        }
    }

    public async Task<TranscriptResult> TranscribeAsync(
        CapturedAudioFile audioFile,
        CancellationToken cancellationToken)
    {
        var options = _optionsAccessor();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Set a Mistral API key in Settings before recording.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/audio/transcriptions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var requestContent = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(audioFile.FilePath);
        using var streamContent = new StreamContent(fileStream);

        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(audioFile.ContentType);

        requestContent.Add(new StringContent(options.TranscriptionModel), "model");
        requestContent.Add(streamContent, "file", audioFile.FileName);

        if (!string.IsNullOrWhiteSpace(options.TranscriptionLanguage))
        {
            requestContent.Add(new StringContent(options.TranscriptionLanguage.Trim()), "language");
        }

        request.Content = requestContent;

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildErrorMessage(payload, response.ReasonPhrase));
        }

        var result = JsonSerializer.Deserialize<MistralTranscriptionResponse>(payload, SerializerOptions);
        var transcriptText = result?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new InvalidOperationException("The transcription service returned an empty transcript.");
        }

        return new TranscriptResult(transcriptText, DateTimeOffset.UtcNow);
    }

    private static string BuildErrorMessage(string payload, string? fallbackReason)
    {
        try
        {
            var errorEnvelope = JsonSerializer.Deserialize<MistralErrorEnvelope>(payload, SerializerOptions);
            if (!string.IsNullOrWhiteSpace(errorEnvelope?.Error?.Message))
            {
                return errorEnvelope.Error.Message;
            }

            if (!string.IsNullOrWhiteSpace(errorEnvelope?.Message))
            {
                return errorEnvelope.Message;
            }
        }
        catch
        {
            // Fall back to the HTTP status reason if JSON parsing fails.
        }

        return string.IsNullOrWhiteSpace(fallbackReason)
            ? "Mistral transcription request failed."
            : $"Mistral transcription request failed: {fallbackReason}";
    }

    private sealed record MistralTranscriptionResponse(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record MistralErrorEnvelope(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("error")] MistralError? Error);

    private sealed record MistralError(
        [property: JsonPropertyName("message")] string? Message);
}
