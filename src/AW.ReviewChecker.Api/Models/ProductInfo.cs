namespace AW.ReviewChecker.Api.Models;

public class ProductInfo
{
    public string Asin { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string AverageRating { get; set; } = string.Empty;
    public string TotalReviews { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
}
