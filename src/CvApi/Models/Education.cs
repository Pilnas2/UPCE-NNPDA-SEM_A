using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record Education
{
    [MaxLength(300)]
    public string? School { get; init; }

    [MaxLength(300)]
    public string? Field { get; init; }

    [MaxLength(100)]
    public string? Degree { get; init; }

    public Education()
    {
    }

    public Education(
        string? school,
        string? field,
        string? degree)
    {
        School = school;
        Field = field;
        Degree = degree;
    }
}
