namespace CvApi;

public sealed record CvResponse(
    long Id,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CvData Data);
