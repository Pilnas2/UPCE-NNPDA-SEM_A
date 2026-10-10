namespace CvApi;

public sealed record ExtractResult(
    string Text,
    string SafeText,
    CheckResult Check);
