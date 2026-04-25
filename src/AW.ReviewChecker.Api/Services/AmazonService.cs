using System.Text.RegularExpressions;
using AW.ReviewChecker.Api.Models;
using HtmlAgilityPack;

namespace AW.ReviewChecker.Api.Services;

public class AmazonService : IAmazonService
{
    private static readonly Regex AsinRegex = new(@"(?<![A-Z0-9])([A-Z0-9]{10})(?![A-Z0-9])", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<AmazonService> _logger;

    public AmazonService(HttpClient httpClient, ILogger<AmazonService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(ProductInfo Product, List<ReviewInfo> Reviews)> GetProductWithReviewsAsync(string productInput)
    {
        var asin = ExtractAsin(productInput)
            ?? throw new ArgumentException($"Could not extract a valid Amazon ASIN from: {productInput}");

        var productUrl = $"https://www.amazon.com/dp/{asin}";
        var reviewsUrl = $"https://www.amazon.com/product-reviews/{asin}?sortBy=recent&pageSize=10";

        _logger.LogInformation("Fetching product page: {Url}", Sanitize(productUrl));
        var productHtml = await FetchHtmlAsync(productUrl);

        _logger.LogInformation("Fetching reviews page: {Url}", Sanitize(reviewsUrl));
        var reviewsHtml = await FetchHtmlAsync(reviewsUrl);

        var product = ParseProduct(asin, productUrl, productHtml);
        var reviews = ParseReviews(reviewsHtml);

        return (product, reviews);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ASIN extraction
    // ──────────────────────────────────────────────────────────────────────────

    internal static string? ExtractAsin(string input)
    {
        input = input.Trim();

        // Try to parse as a URL first
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            // Match /dp/ASIN or /product-reviews/ASIN or /gp/product/ASIN
            var dpMatch = Regex.Match(uri.AbsolutePath,
                @"(?:/dp/|/product-reviews/|/gp/product/)([A-Z0-9]{10})", RegexOptions.IgnoreCase);
            if (dpMatch.Success)
                return dpMatch.Groups[1].Value.ToUpperInvariant();
        }

        // Bare ASIN: exactly 10 alphanumeric characters
        if (Regex.IsMatch(input, @"^[A-Z0-9]{10}$", RegexOptions.IgnoreCase))
            return input.ToUpperInvariant();

        // Last-ditch: first 10-char match anywhere in the string
        var anyMatch = AsinRegex.Match(input.ToUpperInvariant());
        return anyMatch.Success ? anyMatch.Groups[1].Value : null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HTTP helper
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<string> FetchHtmlAsync(string url)
    {
        using var response = await _httpClient.GetAsync(url);
        _logger.LogInformation("GET {Url} → {Status}", Sanitize(url), response.StatusCode);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Product page parsing
    // ──────────────────────────────────────────────────────────────────────────

    private static ProductInfo ParseProduct(string asin, string productUrl, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = GetNodeText(doc, "#productTitle")
            ?? GetNodeText(doc, "#title")
            ?? string.Empty;

        var price = GetNodeText(doc, ".a-price .a-offscreen")
            ?? GetNodeText(doc, "#priceblock_ourprice")
            ?? GetNodeText(doc, "#priceblock_dealprice")
            ?? GetNodeText(doc, "#corePriceDisplay_desktop_feature_div .a-offscreen")
            ?? string.Empty;

        var imageUrl = doc.DocumentNode
            .SelectSingleNode("//img[@id='landingImage']")?
            .GetAttributeValue("src", string.Empty)
            ?? doc.DocumentNode
            .SelectSingleNode("//img[@id='imgTagWrapperId']")?
            .GetAttributeValue("src", string.Empty)
            ?? string.Empty;

        var avgRating = GetNodeText(doc, "#acrPopover span.a-icon-alt")
            ?? GetNodeText(doc, "span[data-hook='rating-out-of-text']")
            ?? string.Empty;

        var totalReviews = GetNodeText(doc, "#acrCustomerReviewText")
            ?? string.Empty;

        return new ProductInfo
        {
            Asin = asin,
            Title = title.Trim(),
            Price = price.Trim(),
            ImageUrl = imageUrl.Trim(),
            AverageRating = avgRating.Trim(),
            TotalReviews = totalReviews.Trim(),
            ProductUrl = productUrl
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Reviews page parsing
    // ──────────────────────────────────────────────────────────────────────────

    private static List<ReviewInfo> ParseReviews(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var reviewNodes = doc.DocumentNode
            .SelectNodes("//*[@data-hook='review']") ?? new HtmlNodeCollection(null);

        var reviews = new List<ReviewInfo>();

        foreach (var node in reviewNodes)
        {
            var reviewId = node.GetAttributeValue("id", string.Empty);

            var author = node
                .SelectSingleNode(".//*[contains(@class,'a-profile-name')]")?
                .InnerText.Trim() ?? string.Empty;

            // Title is the last (non-empty) span inside the title anchor
            var titleNode = node.SelectSingleNode(".//*[@data-hook='review-title']");
            var titleText = titleNode?.SelectNodes(".//span")?
                .LastOrDefault(s => !string.IsNullOrWhiteSpace(s.InnerText))?
                .InnerText.Trim()
                ?? titleNode?.InnerText.Trim()
                ?? string.Empty;

            var bodyNode = node.SelectSingleNode(".//*[@data-hook='review-body']");
            var body = bodyNode?.SelectSingleNode(".//span")?.InnerText.Trim()
                ?? bodyNode?.InnerText.Trim()
                ?? string.Empty;

            var ratingNode = node.SelectSingleNode(".//*[@data-hook='review-star-rating']")
                ?? node.SelectSingleNode(".//*[@data-hook='cmps-review-star-rating']");
            var rating = ratingNode?.SelectSingleNode(".//span[@class='a-icon-alt']")?
                .InnerText.Trim() ?? string.Empty;

            var date = node
                .SelectSingleNode(".//*[@data-hook='review-date']")?
                .InnerText.Trim() ?? string.Empty;

            var verified = node
                .SelectSingleNode(".//*[@data-hook='avp-badge']") is not null;

            reviews.Add(new ReviewInfo
            {
                ReviewId = reviewId,
                Author = author,
                Title = HtmlEntity.DeEntitize(titleText),
                Body = HtmlEntity.DeEntitize(body),
                Rating = rating,
                Date = date,
                VerifiedPurchase = verified
            });
        }

        return reviews;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string? GetNodeText(HtmlDocument doc, string cssSelector)
    {
        // HtmlAgilityPack doesn't support CSS selectors natively; convert the
        // most common patterns to XPath.
        var xpath = CssToXPath(cssSelector);
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return string.IsNullOrWhiteSpace(node?.InnerText) ? null : node.InnerText;
    }

    private static string CssToXPath(string css)
    {
        // Handle "#id" → //*[@id='id']
        if (css.StartsWith('#'))
            return $"//*[@id='{css[1..]}']";

        // Handle ".class" → //*[contains(@class,'class')]
        if (css.StartsWith('.'))
            return $"//*[contains(@class,'{css[1..]}')]";

        // Handle "tag#id"
        var tagIdMatch = Regex.Match(css, @"^(\w+)#(.+)$");
        if (tagIdMatch.Success)
            return $"//{tagIdMatch.Groups[1].Value}[@id='{tagIdMatch.Groups[2].Value}']";

        // Handle "ancestor .descendant"
        if (css.Contains(' '))
        {
            var parts = css.Split(' ', 2);
            return $"{CssToXPath(parts[0])}//{CssToXPath(parts[1]).TrimStart('/')}";
        }

        // Handle "tag[attr='value']"
        var attrMatch = Regex.Match(css, @"^(\w+)\[([^\]]+)\]$");
        if (attrMatch.Success)
            return $"//{attrMatch.Groups[1].Value}[@{attrMatch.Groups[2].Value}]";

        return $"//{css}";
    }

    private static string Sanitize(string value) =>
        value.Replace('\n', ' ').Replace('\r', ' ');
}
