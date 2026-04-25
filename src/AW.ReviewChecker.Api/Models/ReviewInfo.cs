namespace AW.ReviewChecker.Api.Models;

public class ReviewInfo
{
    public string ReviewId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public bool VerifiedPurchase { get; set; }
}
