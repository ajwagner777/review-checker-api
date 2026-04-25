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

        var analyses = new List<ReviewAnalysis>();

        foreach (var review in reviews)
        {
            var textToAnalyze = string.IsNullOrWhiteSpace(review.Body)
                ? review.Title
                : review.Body;

            var (score, reasoning) = await _openAiService.AnalyzeReviewAsync(textToAnalyze);

            analyses.Add(new ReviewAnalysis
            {
                Review = review,
                AiScore = score,
                AiReasoning = reasoning
            });
        }

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
