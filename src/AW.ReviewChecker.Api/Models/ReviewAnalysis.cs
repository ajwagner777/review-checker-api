namespace AW.ReviewChecker.Api.Models;

public class ReviewAnalysis
{
    public ReviewInfo Review { get; set; } = new();

    /// <summary>
    /// AI-generated likelihood score: 1 = definitely human, 10 = definitely AI.
    /// </summary>
    public int AiScore { get; set; }

    /// <summary>
    /// Brief explanation of the AI score.
    /// </summary>
    public string AiReasoning { get; set; } = string.Empty;
}
