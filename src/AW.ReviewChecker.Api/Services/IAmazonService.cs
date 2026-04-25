using AW.ReviewChecker.Api.Models;

namespace AW.ReviewChecker.Api.Services;

public interface IAmazonService
{
    /// <summary>
    /// Given an Amazon product URL or a bare ASIN, returns the product information
    /// along with the first page of customer reviews.
    /// </summary>
    Task<(ProductInfo Product, List<ReviewInfo> Reviews)> GetProductWithReviewsAsync(string productInput);
}
