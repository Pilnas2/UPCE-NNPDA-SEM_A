using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CvApi;

public static class InputCheck
{
    private static readonly Regex Injection = new(
        @"ignore\s+(all\s+)?(previous|prior|above)|ignoruj\w*\s+(všechny\s+)?(předchozí|instrukce)|rank\s+me|dej\s+m[eě]\s+na\s+prvn|ohodnoť\s+m[eě]|maximum\s+(score|points)|system\s*prompt|\[/?INST\]|<\|.*?\|>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool IsInstruction(string text) => Injection.IsMatch(text);
    public static string Clean(string text) => string.Join('\n', text.Split(['\r', '\n']).Where(x => !IsInstruction(x)));
    public static string? CleanField(string? text) => text is null || IsInstruction(text) ? null : text;
    // clean whole object
    public static void Sanitize(CvData data)
    {
        foreach (var p in typeof(CvData).GetProperties().Where(p => p.PropertyType == typeof(string)))
            p.SetValue(data, CleanField((string?)p.GetValue(data)));
        data.Languages = data.Languages.Where(x => !IsInstruction(x.LanguageName)).ToList();
        data.DrivingLicenses = data.DrivingLicenses.Where(x => !IsInstruction(x)).ToList();
        data.WorkExperience = data.WorkExperience.Select(x => x with { Position = CleanField(x.Position), Company = CleanField(x.Company) }).ToList();
        data.Education = data.Education.Select(x => x with { School = CleanField(x.School), Field = CleanField(x.Field), Degree = CleanField(x.Degree) }).ToList();
    }
    // check if value is equal CvData object
    public static void ValidateObject(object value)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new(value), results, true))
            throw new ApiException(502, "OpenAI vrátilo data mimo povolenou strukturu.");
        if (value is CvData cv)
        {
            foreach (var item in cv.Languages.Cast<object>().Concat(cv.WorkExperience).Concat(cv.Education)) ValidateObject(item);
        }
    }
}
