using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed class CvData : IValidatableObject
{
    [MaxLength(200)]
    public string? FirstName { get; set; }

    [MaxLength(200)]
    public string? LastName { get; set; }


    [EmailAddress]
    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(60)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? City { get; set; }

    [MaxLength(200)]
    public string? Region { get; set; }

    [MaxLength(30)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Nationality { get; set; }

    [MaxLength(100)]
    public string? Gender { get; set; }

    [Required]
    [MaxLength(50)]
    public List<Language> Languages { get; set; } = new List<Language>();

    [Required]
    [MaxLength(30)]
    public List<string> DrivingLicenses { get; set; } = new List<string>();

    [Required]
    [MaxLength(2)]
    public List<WorkExperience> WorkExperience { get; set; } = new List<WorkExperience>();

    [Required]
    [MaxLength(50)]
    public List<Education> Education { get; set; } = new List<Education>();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var hasNullLanguage = Languages?.Any(language => language is null) == true;
        var hasNullWorkExperience = WorkExperience?.Any(experience => experience is null) == true;
        var hasNullEducation = Education?.Any(education => education is null) == true;
        var hasInvalidDrivingLicense = DrivingLicenses?.Any(
            license => string.IsNullOrWhiteSpace(license) || license.Length > 10) == true;

        if (hasNullLanguage ||
            hasNullWorkExperience ||
            hasNullEducation ||
            hasInvalidDrivingLicense)
        {
            yield return new ValidationResult(
                "Kolekce nesmí obsahovat null nebo neplatná řidičská oprávnění.");
        }
    }
}
