using AW.ReviewChecker.Api.Models;
using AW.ReviewChecker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AW.ReviewChecker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewCheckerController : ControllerBase
{
    private readonly IAmazonService _amazonService;
    private readonly IOpenAiService _openAiService;
    private readonly ILogger<ReviewCheckerController> _logger;

    public ReviewCheckerController(
        IAmazonService amazonService,
        IOpenAiService openAiService,
        ILogger<ReviewCheckerController> logger)
    {
        _amazonService = amazonService;
        _openAiService = openAiService;
        _logger = logger;
    }

    /// <summary>
    /// Accepts an Amazon product URL or ASIN, retrieves the product information and
    /// customer reviews, and uses OpenAI to score each review's likelihood of being
    /// AI-generated on a scale of 1 (human) to 10 (AI).
    /// </summary>
    /// <param name="request">Object containing the Amazon product URL or ASIN.</param>
    /// <returns>Product details and per-review AI scores.</returns>
    [HttpPost("check")]
    [ProducesResponseType(typeof(ReviewCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CheckReviews([FromBody] ReviewCheckRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        _logger.LogInformation("Review check requested for: {Input}", Sanitize(request.ProductInput));

        ProductInfo product;
        List<ReviewInfo> reviews;

        try
        {
            (product, reviews) = await _amazonService.GetProductWithReviewsAsync(request.ProductInput);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid product input.");
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid product input",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch data from Amazon.");
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Amazon fetch failed",
                Detail = "Could not retrieve product data from Amazon. " +
                         "Amazon may be blocking automated requests. " + ex.Message,
                Status = StatusCodes.Status502BadGateway
            });
        }

        var reviewsToAnalyze = reviews
            .Where(r => r.Rating.StartsWith("5", StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        _logger.LogInformation(
            "Analyzing {Count} 5-star reviews (capped at 10) out of {Total} total reviews.",
            reviewsToAnalyze.Count, reviews.Count);

        var analysisTasks = reviewsToAnalyze.Select(async review =>
        {
            var textToAnalyze = string.IsNullOrWhiteSpace(review.Body)
                ? review.Title
                : review.Body;

            int score;
            string reasoning;

            try
            {
                (score, reasoning) = await _openAiService.AnalyzeReviewAsync(textToAnalyze);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OpenAI analysis failed for review {ReviewId}. Using neutral fallback score.",
                    review.ReviewId);

                score = 5;
                reasoning = "AI analysis unavailable. Returned neutral score.";
            }

            return new ReviewAnalysis
            {
                Review = review,
                AiScore = score,
                AiReasoning = reasoning
            };
        });

        var analyses = (await Task.WhenAll(analysisTasks)).ToList();

        var averageScore = analyses.Count > 0
            ? Math.Round(analyses.Average(a => a.AiScore), 2)
            : 0;

        var response = new ReviewCheckResponse
        {
            Product = product,
            Reviews = analyses,
            AverageAiScore = averageScore
        };

        return Ok(response);
    }

    private static string Sanitize(string value) =>
        value.Replace('\n', ' ').Replace('\r', ' ');
}
