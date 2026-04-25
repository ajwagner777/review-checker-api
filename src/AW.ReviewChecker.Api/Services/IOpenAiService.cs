using AW.ReviewChecker.Api.Models;

namespace AW.ReviewChecker.Api.Services;

public interface IOpenAiService
{
    /// <summary>
    /// Analyzes a single review text and returns an AI-likelihood score (1–10)
    /// plus a brief reasoning string.
    /// </summary>
    Task<(int Score, string Reasoning)> AnalyzeReviewAsync(string reviewText);
}
