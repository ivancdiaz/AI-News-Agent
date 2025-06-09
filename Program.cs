using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using AI.News.Agent.Services;
using AI.News.Agent.Output;
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

                // 🟢 Setup DI container via new method
                var serviceProvider = ConfigureServices(config, apiKey, huggingFaceApiKey); // 🟢

                // Build service and ensure DisposeAsync() is called for PlaywrightRenderService
                // 🟢 Ensure async dispose for services like PlaywrightRenderService
                await using (serviceProvider as IAsyncDisposable)
                {

                    // 🟢 Optional: get logger for Program if needed
                    var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // 🟢
                    logger.LogInformation("News Agent initialized. External APIs configured. Ready to process input."); // 🟢

                    // Validate both API keys are present and not empty
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        logger.LogError("NewsAPI key is missing from configuration. Exiting program."); // 🟢
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(huggingFaceApiKey))
                    {
                        logger.LogError("Hugging Face API key is missing from configuration. Exiting program."); // 🟢
                        return;
                    }

                    // Retrieve NewsApiService from DI and fetch news
                    var newsService = serviceProvider.GetRequiredService<NewsApiService>();
                    var outputService = new OutputService();

                    // Fetch and display top headlines first
                    var newsResult = await newsService.FetchTopHeadlinesAsync();
                    if (newsResult.Success)
                    {
                        outputService.DisplayArticles(newsResult.Articles);
                    }
                    else
                    {
                        logger.LogError(
                            "Failed to fetch top headlines: {Message}",
                            newsResult.ErrorMessage); // 🟢 Using error message from result
                        return;
                    }

                    // Get ArticleBodyService with dependencies
                    var articleBodyService = serviceProvider.GetRequiredService<ArticleBodyService>();

                    // Prompt for the URL
                    WriteLine("Please enter the URL of the article:"); // Future update: Select URL from NewsAPI list
                    string testUrl = ReadLine();

                    if (string.IsNullOrEmpty(testUrl))
                    {
                        logger.LogWarning("No URL was provided. Exiting."); // 🟢
                        return;
                    }

                    // Get the article body
                    ArticleBodyResult result = await articleBodyService.GetArticleBodyAsync(testUrl);
                    if (result.Success)
                    {
                        // Flipped to see if it fixes output cross stream // 🟢
                        WriteLine($"\nArticle Body:\n{result.ArticleBody}\n");

                        logger.LogInformation(
                            "Retrieved article body. Length: {Length} characters",
                            result.ArticleBody.Length); // 🟢

                        // AIAnalysisService
                        var aiAnalysisService = serviceProvider.GetRequiredService<IAIAnalysisService>();

                        // Log input length before summarization
                        logger.LogInformation(
                            "Passing article to AI summarization. Length: {CharCount} characters",
                            result.ArticleBody.Length); // 🟢

                        // Summarize the article text
                        var summary = await aiAnalysisService.SummarizeArticleAsync(result.ArticleBody);
                        WriteLine($"\nAI Summary:\n{summary}");
                        /*
                        logger.LogInformation(
                            "\nAI Summary:\n{Summary}\n",
                            summary);  // 🟢
                        */
                    }
                    else
                    {
                        logger.LogError(
                            "Failed to retrieve article body: {Message}",
                            result.ErrorMessage); // 🟢
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

        // 🟢 New method to configure DI container and register services
        static ServiceProvider ConfigureServices(IConfiguration config, string apiKey, string huggingFaceApiKey) // 🟢
        {
            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient(); // Register IHttpClientFactory

            // 🟢 Add logging
            services.AddLogging(logging =>
            {
                /*
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddConfiguration(config.GetSection("Logging")); // 🟢 Apply appsettings.json log level settings
                */
                logging.ClearProviders(); // 🟢 Remove all previously registered logging providers to start fresh
                logging.AddConsole(options =>
                {
                    // 🟢 Configure console logger to send all log levels to standard error (stderr)
                    // 🟢 This helps separate logs from regular console output (which goes to stdout)
                    options.LogToStandardErrorThreshold = LogLevel.Trace; // 🟢 Route all logs to stderr
                });
                logging.AddConfiguration(config.GetSection("Logging"));
            });

            // 🟢 Optional: Add configuration to DI (helpful if other services need it)
            services.AddSingleton<IConfiguration>(config);

            // Register PlaywrightRenderService as a singleton
            services.AddSingleton<IPlaywrightRenderService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<PlaywrightRenderService>>(); // 🟢 Inject logger
                return new PlaywrightRenderService(logger); // 🟢 Pass logger
            });

            // Register NewsApiService with NewsAPI key
            services.AddTransient<NewsApiService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var logger = provider.GetRequiredService<ILogger<NewsApiService>>(); // 🟢 Inject logger
                return new NewsApiService(factory, apiKey, logger); // 🟢 Pass logger
            });

            // Register NewsService
            services.AddTransient<NewsService>();

            // Register AIAnalysisService with Hugging Face API key and 🟢 with logger injection
            services.AddTransient<IAIAnalysisService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var logger = provider.GetRequiredService<ILogger<AIAnalysisService>>(); // 🟢 Inject logger
                return new AIAnalysisService(factory, huggingFaceApiKey, logger); // 🟢 Pass logger
            });

            // Register ArticleBodyService with its dependencies (HttpClientFactory + PlaywrightRenderService)
            services.AddTransient<ArticleBodyService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                var playwright = provider.GetRequiredService<IPlaywrightRenderService>();
                var logger = provider.GetRequiredService<ILogger<ArticleBodyService>>(); // 🟢 Inject logger
                return new ArticleBodyService(factory, playwright, logger); // 🟢 Pass logger
            });

            // 🟢 Return the built service provider!
            return services.BuildServiceProvider();
        }
    }
}