using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed record LoginRequest(
    [Required]
    string Username,
    [Required]
    string Password);
