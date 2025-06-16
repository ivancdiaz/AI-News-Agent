using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using AI.News.Agent.Services;
using AI.News.Agent.Output;
using AI.News.Agent.Models;
using static System.Console;

namespace AI.News.Agent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // Load configuration from appsettings.json
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                string apiKey = config["ApiKeys:NewsApiKey"];
                string huggingFaceApiKey = config["ApiKeys:HuggingFaceApiKey"];

                // Setup DI container via new method
                var serviceProvider = ConfigureServices(config, apiKey, huggingFaceApiKey);

                // Build service and ensure DisposeAsync() is called for PlaywrightRenderService
                await using (serviceProvider as IAsyncDisposable)
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("News Agent initialized. External APIs configured. Ready to process input.");

                    // Validate both API keys are present and not empty
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        logger.LogError("NewsAPI key is missing from configuration. Exiting program.");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(huggingFaceApiKey))
                    {
                        logger.LogError("Hugging Face API key is missing from configuration. Exiting program.");
                        return;
                    }

                    // Fetch news using NewsApiService and handle Result<T> appropriately
                    var newsService = serviceProvider.GetRequiredService<NewsApiService>();
                    var outputService = new OutputService();

                    // Fetch and display top headlines first
                    var newsResult = await newsService.FetchTopHeadlinesAsync();
                    if (newsResult.Success)
                    {
                        outputService.DisplayArticles(newsResult.Value);
                    }
                    else
                    {
                        logger.LogError(
                            "Failed to fetch top headlines: {Message}",
                            newsResult.ErrorMessage);
                        return;
                    }

                    // Get ArticleBodyService with dependencies
                    var articleBodyService = serviceProvider.GetRequiredService<ArticleBodyService>();

                    // Prompt for the URL
                    WriteLine("Please enter the URL of the article:"); // Future update: Select URL from NewsAPI list
                    string testUrl = ReadLine();

                    if (string.IsNullOrEmpty(testUrl))
                    {
                        logger.LogWarning("No URL was provided. Exiting.");
                        return;
                    }

                    // Get the article body and handle Result<T>
                    var articleBodyResult = await articleBodyService.GetArticleBodyAsync(testUrl);

                    // Exit early if article fetch fails
                    if (!articleBodyResult.Success)
                    {
                        logger.LogError(
                            "Failed to retrieve article body: {Message}",
                            articleBodyResult.ErrorMessage);
                        return;
                    }

                    WriteLine($"\nArticle Body:\n{articleBodyResult.Value.Text}\n");

                    logger.LogInformation(
                        "Retrieved article body. Length: {Length} characters",
                        articleBodyResult.Value.Text.Length);

                    // AI summarization service
                    var aiAnalysisService = serviceProvider.GetRequiredService<IAIAnalysisService>();

                    logger.LogInformation(
                        "Passing article to AI summarization. Length: {CharCount} characters",
                        articleBodyResult.Value.Text.Length);

                    Result<Summary> summaryResult;

                    try
                    {
                        // Get the summarization result from the AI service
                        summaryResult = await aiAnalysisService.SummarizeArticleAsync(articleBodyResult.Value.Text);

                        // Check if the summarization was successful
                        if (summaryResult.Success)
                        {
                            // Extract the summary text and proceed
                            var summaryText = summaryResult.Value.Text;
                            WriteLine($"\nAI Summary:\n{summaryText}\n");
                        }
                        else
                        {
                            logger.LogError(
                                "Failed to summarize article: {Message}", 
                                summaryResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "AI summarization failed.");
                        return; // Exit on summarization failure
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback logger if DI logger is not available before serviceProvider is built
                using var fallbackLoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                var fallbackLogger = fallbackLoggerFactory.CreateLogger<Program>();
                fallbackLogger.LogCritical(ex, "Unhandled exception occurred in the main application loop.");
            }
        }

        // Configure DI container and register services
        static ServiceProvider ConfigureServices(IConfiguration config, string apiKey, string huggingFaceApiKey)
        {
            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient(); // Register IHttpClientFactory

            // Add logging to the DI container
            services.AddLogging(logging =>
            {
                logging.ClearProviders(); // Remove existing logging providers
                logging.AddConsole(options =>
                {
                    // Send all console logs to stderr to avoid overlap with stdout messages
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
                logging.AddConfiguration(config.GetSection("Logging"));
            });

            // Add IConfiguration to DI if other services need it
            services.AddSingleton<IConfiguration>(config);

            // Register PlaywrightRenderService as a singleton
            services.AddSingleton<IPlaywrightRenderService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<PlaywrightRenderService>>();
                return new PlaywrightRenderService(logger);
            });

            // Register NewsApiService with NewsAPI key
            services.AddTransient<NewsApiService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var logger = provider.GetRequiredService<ILogger<NewsApiService>>();
                return new NewsApiService(factory, apiKey, logger);
            });

            // Register NewsService
            services.AddTransient<NewsService>();

            // Register AIAnalysisService with Hugging Face API key and with logger injection
            services.AddTransient<IAIAnalysisService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var logger = provider.GetRequiredService<ILogger<AIAnalysisService>>();
                return new AIAnalysisService(factory, huggingFaceApiKey, logger);
            });

            // Register ArticleBodyService with its dependencies (HttpClientFactory + PlaywrightRenderService)
            services.AddTransient<ArticleBodyService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var playwright = provider.GetRequiredService<IPlaywrightRenderService>();
                var logger = provider.GetRequiredService<ILogger<ArticleBodyService>>();
                return new ArticleBodyService(factory, playwright, logger);
            });

            return services.BuildServiceProvider();
        }
    }
}