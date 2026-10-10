using System.ComponentModel.DataAnnotations;

namespace CvApi;

public sealed class UploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
