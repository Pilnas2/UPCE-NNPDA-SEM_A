using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record ParseRequest(
    [Required]
    string Text);
