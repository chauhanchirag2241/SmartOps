namespace SmartOps.Infrastructure.Modules.StaffAttendance;

public sealed class FaceServiceOptions
{
    public const string SectionName = "FaceService";

    public string BaseUrl { get; set; } = "http://localhost:8090";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Minimum cosine/similarity score required for a face match.</summary>
    public float MatchThreshold { get; set; } = 0.4f;
}
