namespace CvApi;

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt);
