using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record WorkExperience
{
    [MaxLength(300)]
    public string? Position { get; init; }

    [MaxLength(300)]
    public string? Company { get; init; }

    [RegularExpression(@"^\d{4}(-(0[1-9]|1[0-2]))?$")]
    public string? From { get; init; }

    [RegularExpression(@"^\d{4}(-(0[1-9]|1[0-2]))?$")]
    public string? To { get; init; }

    public WorkExperience()
    {
    }

    public WorkExperience(
        string? position,
        string? company,
        string? from,
        string? to)
    {
        Position = position;
        Company = company;
        From = from;
        To = to;
    }
}
