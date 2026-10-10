using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record AnalysisRequest(
    [Required]
    [StringLength(30000, MinimumLength = 1)]
    string JobDescription);
