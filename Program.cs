using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AI.News.Agent.Services;
using AI.News.Agent.Config;

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
builder.Services.Configure<AIModelSettings>(builder.Configuration.GetSection("AI:Models"));

// Register services
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IPlaywrightRenderService, PlaywrightRenderService>();

builder.Services.AddTransient<NewsApiService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<NewsApiService>>();
    var apiSettings = provider.GetRequiredService<IOptions<ApiSettings>>().Value;
    var apiKeys = provider.GetRequiredService<IOptions<ApiKeySettings>>().Value;
    return new NewsApiService(factory, apiKeys.NewsApiKey, apiSettings.NewsApiBaseUrl, logger);
});

builder.Services.AddTransient<ArticleBodyService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var renderer = provider.GetRequiredService<IPlaywrightRenderService>();
    var logger = provider.GetRequiredService<ILogger<ArticleBodyService>>();
    return new ArticleBodyService(factory, renderer, logger);
});

builder.Services.AddTransient<IAIAnalysisService>(provider =>
{
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<AIAnalysisService>>();
    var modelSettings = provider.GetRequiredService<IOptions<AIModelSettings>>().Value;
    var apiKeys = provider.GetRequiredService<IOptions<ApiKeySettings>>().Value;
    return new AIAnalysisService(factory, apiKeys.HuggingFaceApiKey, logger, modelSettings.Primary);
});

// Add controllers and swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Config middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();