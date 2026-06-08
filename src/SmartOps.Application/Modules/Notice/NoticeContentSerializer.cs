using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartOps.Application.Modules.Notice;

public static class NoticeContentSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string? Serialize(NoticeContentPayloadDto? content)
    {
        return content is null ? null : JsonSerializer.Serialize(content, Options);
    }

    public static NoticeContentPayloadDto Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new NoticeContentPayloadDto();
        }

        try
        {
            return JsonSerializer.Deserialize<NoticeContentPayloadDto>(json, Options) ?? new NoticeContentPayloadDto();
        }
        catch
        {
            return new NoticeContentPayloadDto();
        }
    }
}
