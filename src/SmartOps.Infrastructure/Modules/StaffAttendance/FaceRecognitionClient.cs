using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;

namespace SmartOps.Infrastructure.Modules.StaffAttendance;

public sealed class FaceRecognitionClient : IFaceRecognitionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly FaceServiceOptions _options;
    private readonly ILogger<FaceRecognitionClient> _logger;

    public FaceRecognitionClient(
        HttpClient http,
        IOptions<FaceServiceOptions> options,
        ILogger<FaceRecognitionClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FaceEmbedResult> EnrollEmbeddingAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(imageContent, "image", "face.jpg");

        using HttpResponseMessage response = await _http.PostAsync("/v1/embed", content, ct).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Face embed failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Face embedding failed: {(int)response.StatusCode}");
        }

        EmbedResponse? parsed = JsonSerializer.Deserialize<EmbedResponse>(body, JsonOptions);
        if (parsed?.Embedding is null || parsed.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Face embedding response was empty.");
        }

        return new FaceEmbedResult(parsed.Embedding, parsed.Model ?? "buffalo_l");
    }

    public async Task<FaceMatchResult?> MatchAsync(
        float[] probeEmbedding,
        IReadOnlyList<FaceMatchCandidate> candidates,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var payload = new MatchRequest
        {
            Embedding = probeEmbedding,
            Candidates = candidates.Select(c => new MatchCandidateDto
            {
                EmployeeId = c.EmployeeId,
                Embedding = c.Embedding
            }).ToList(),
            Threshold = _options.MatchThreshold
        };

        using HttpResponseMessage response = await _http.PostAsJsonAsync("/v1/match", payload, JsonOptions, ct)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Face match failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Face match failed: {(int)response.StatusCode}");
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim() is "null" or "{}")
        {
            return null;
        }

        MatchResponse? parsed = JsonSerializer.Deserialize<MatchResponse>(body, JsonOptions);
        if (parsed is null || parsed.EmployeeId == Guid.Empty)
        {
            return null;
        }

        if (parsed.Score < _options.MatchThreshold)
        {
            return null;
        }

        return new FaceMatchResult(parsed.EmployeeId, parsed.Score);
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    private sealed class MatchRequest
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];

        [JsonPropertyName("candidates")]
        public List<MatchCandidateDto> Candidates { get; set; } = [];

        [JsonPropertyName("threshold")]
        public float Threshold { get; set; }
    }

    private sealed class MatchCandidateDto
    {
        [JsonPropertyName("employeeId")]
        public Guid EmployeeId { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }

    private sealed class MatchResponse
    {
        [JsonPropertyName("employeeId")]
        public Guid EmployeeId { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }
    }
}
