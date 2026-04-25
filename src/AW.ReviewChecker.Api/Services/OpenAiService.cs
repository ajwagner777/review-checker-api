using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI;
using OpenAI.Chat;

namespace AW.ReviewChecker.Api.Services;

public class OpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<OpenAiService> _logger;

    private const string SystemPrompt =
        "You are an expert at detecting AI-generated text. " +
        "When given an Amazon product review, you respond with ONLY a valid JSON object " +
        "containing exactly two fields: \"score\" (integer 1-10) and \"reasoning\" (string). " +
        "Score meanings: 1 = definitely written by a human, 10 = definitely written by AI. " +
        "Do not include any text outside the JSON object.";

    private const string UserPromptTemplate =
        "Analyze the following Amazon product review and score it on how likely it is to be " +
        "AI-generated (1 = definitely human, 10 = definitely AI). " +
        "Consider: natural vs formulaic language, specific personal experiences vs generic statements, " +
        "emotional authenticity, grammar patterns, and review structure.\n\n" +
        "Review:\n\"\"\"\n{0}\n\"\"\"";

    public OpenAiService(IConfiguration configuration, ILogger<OpenAiService> logger)
    {
        _logger = logger;

        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var openAiClient = new OpenAIClient(apiKey);
        _chatClient = openAiClient.GetChatClient(model);
    }

    public async Task<(int Score, string Reasoning)> AnalyzeReviewAsync(string reviewText)
    {
        if (string.IsNullOrWhiteSpace(reviewText))
            return (5, "No review text provided.");

        var userMessage = string.Format(UserPromptTemplate, reviewText);

        _logger.LogDebug("Sending review to OpenAI for analysis.");

        var completion = await _chatClient.CompleteChatAsync(
        [
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userMessage)
        ]);

        var responseText = completion.Value.Content[0].Text;
        _logger.LogDebug("OpenAI response: {Response}", responseText);

        return ParseResponse(responseText);
    }

    private (int Score, string Reasoning) ParseResponse(string responseText)
    {
        try
        {
            // Strip possible markdown code fences
            var json = responseText.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            var result = JsonSerializer.Deserialize<AiAnalysisResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null)
                return (5, "Could not parse OpenAI response.");

            var score = Math.Clamp(result.Score, 1, 10);
            return (score, result.Reasoning ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAI JSON response: {Response}", responseText);
            return (5, $"Parse error – raw response: {responseText}");
        }
    }

    private sealed class AiAnalysisResult
    {
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }
    }
}
