namespace CvApi;

public sealed record Issue(
    string Type,
    string Text,
    double? FontSize = null,
    string? Location = null);
