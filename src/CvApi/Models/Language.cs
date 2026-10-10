using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record Language
{
    [Required]
    [MaxLength(100)]
    [System.Text.Json.Serialization.JsonPropertyName("language")]
    public string LanguageName { get; init; } = "";

    [RegularExpression("^(A1|A2|B1|B2|C1|C2)$")]
    public string? Level { get; init; }

    public Language()
    {
    }

    public Language(
        string languageName,
        string? level)
    {
        LanguageName = languageName;
        Level = level;
    }
}
