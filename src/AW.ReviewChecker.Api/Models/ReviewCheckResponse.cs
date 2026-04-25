namespace AW.ReviewChecker.Api.Models;

public class ReviewCheckResponse
{
    public ProductInfo Product { get; set; } = new();
    public List<ReviewAnalysis> Reviews { get; set; } = [];

    /// <summary>
    /// Average AI score across all analyzed reviews (1 = human, 10 = AI).
    /// </summary>
    public double AverageAiScore { get; set; }
}
