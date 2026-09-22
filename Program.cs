using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using AI.News.Agent.Services;
using AI.News.Agent.Config;
using System.Reflection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Load config and environment
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Setup logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Config bindings
builder.Services.Configure<ApiKeySettings>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<ArticleSummarizationSettings>(builder.Configuration.GetSection("AI:ArticleSummarization"));
builder.Services.Configure<QueryParserSettings>(builder.Configuration.GetSection("AI:QueryParser"));

// Register services
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IPlaywrightRenderService, PlaywrightRenderService>();

// Query parsing uses Qwen3-32B via Hugging Face Inference Providers.
// MockQueryParserService remains in the codebase; to switch back, register it here instead.
builder.Services.AddTransient<IQueryParserService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<QueryParserService>>();
    var queryParserSettings = provider.GetRequiredService<IOptions<QueryParserSettings>>().Value;
    var apiKeys = provider.GetRequiredService<IOptions<ApiKeySettings>>().Value;
    return new QueryParserService(factory, apiKeys.HuggingFaceApiKey, logger, queryParserSettings);
});

builder.Services.AddTransient<NewsApiService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<NewsApiService>>();
    var apiSettings = provider.GetRequiredService<IOptions<ApiSettings>>().Value;
    var apiKeys = provider.GetRequiredService<IOptions<ApiKeySettings>>().Value;
    return new NewsApiService(factory, apiKeys.NewsApiKey, apiSettings.NewsApiBaseUrl, apiSettings.NewsApiEverythingBaseUrl, logger);
});

builder.Services.AddTransient<ArticleBodyService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var renderer = provider.GetRequiredService<IPlaywrightRenderService>();
    var logger = provider.GetRequiredService<ILogger<ArticleBodyService>>();
    return new ArticleBodyService(factory, renderer, logger);
});

builder.Services.AddTransient<IArticleSummarizationService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<ArticleSummarizationService>>();
    var summarizationSettings = provider.GetRequiredService<IOptions<ArticleSummarizationSettings>>().Value;
    var apiKeys = provider.GetRequiredService<IOptions<ApiKeySettings>>().Value;
    return new ArticleSummarizationService(factory, apiKeys.HuggingFaceApiKey, logger, summarizationSettings.Model);
});

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Swagger with XML comments and custom versioning 
builder.Services.AddSwaggerGen(options =>
{
    // Set versioning and metadata for the API UI
    options.SwaggerDoc("v1.2", new OpenApiInfo
    {
        Version = "v1.2",
        Title = "AI.News.Agent API",
        Description = "API for fetching and extracting news articles, with AI-powered summarization"
    });
    // Add XML comments for Swagger UI
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();

// Config middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Set the versioned JSON endpoint
        c.SwaggerEndpoint("/swagger/v1.2/swagger.json", "AI.News.Agent API v1.2");
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();