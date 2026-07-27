namespace SmartOps.Application.Modules.StaffAttendance.Interfaces;

public interface IFaceRecognitionClient
{
    Task<FaceEmbedResult> EnrollEmbeddingAsync(byte[] imageBytes, CancellationToken ct = default);

    Task<FaceMatchResult?> MatchAsync(
        float[] probeEmbedding,
        IReadOnlyList<FaceMatchCandidate> candidates,
        CancellationToken ct = default);
}
