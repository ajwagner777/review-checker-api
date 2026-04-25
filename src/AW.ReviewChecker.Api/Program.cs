using System.Reflection;
using AW.ReviewChecker.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AW Review Checker API",
        Version = "v1",
        Description =
            "Accepts an Amazon product URL or ASIN, retrieves product information and " +
            "customer reviews, and uses OpenAI to score each review's likelihood of being " +
            "AI-generated on a scale of 1 (definitely human) to 10 (definitely AI)."
    });

    // Include XML comments from this assembly if the file exists
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddHttpClient<IAmazonService, AmazonService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept",
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
});

builder.Services.AddScoped<IOpenAiService, OpenAiService>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Swagger UI (available in all environments) ────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AW Review Checker API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at the root URL
});

app.UseAuthorization();
app.MapControllers();

app.Run();
