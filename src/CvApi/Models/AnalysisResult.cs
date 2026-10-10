namespace CvApi;

public sealed record AnalysisResult(
    string CandidateSummary,
    string[] MatchingSkills,
    string[] MissingSkills);
