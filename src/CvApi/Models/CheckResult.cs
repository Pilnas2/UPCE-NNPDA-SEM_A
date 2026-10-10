namespace CvApi;

public sealed record CheckResult(
    bool Suspicious,
    IReadOnlyList<Issue> Issues);
