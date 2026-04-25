using System.ComponentModel.DataAnnotations;

namespace AW.ReviewChecker.Api.Models;

public class ReviewCheckRequest
{
    /// <summary>
    /// An Amazon product URL (e.g. https://www.amazon.com/dp/B08N5KWB9H)
    /// or a bare ASIN / product number (e.g. B08N5KWB9H).
    /// </summary>
    [Required]
    public string ProductInput { get; set; } = string.Empty;
}
