using Microsoft.Extensions.DependencyInjection;
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
            // Prompt for the API key BEFORE building the DI container
            WriteLine("Please enter your NewsAPI key (or type 'skip' to test only article URL):");
            var apiKey = ReadLine();

            // Setup dependency injection
            var services = new ServiceCollection();

            services.AddHttpClient(); // Register IHttpClientFactory

            // Register PlaywrightRenderService as a singleton
            services.AddSingleton<IPlaywrightRenderService, PlaywrightRenderService>();

            // Register NewsApiService if not skipping
            if (!string.Equals(apiKey, "skip", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    WriteLine("[ERROR] API key is required unless you type 'skip'. Exiting program.");
                    return;
                }

                // Register NewsApiService with NewsAPI key
                services.AddTransient<NewsApiService>(provider =>
                {
                    var factory = provider.GetRequiredService<IHttpClientFactory>();
                    return new NewsApiService(factory, apiKey);
                });

                // Register NewsService only when NewsAPI is used
                services.AddTransient<NewsService>();
            }

            // Prompt for the Hugging Face API key before asking for URL
            WriteLine("Please enter your Hugging Face API key:");
            var huggingFaceApiKey = ReadLine();

            if (string.IsNullOrWhiteSpace(huggingFaceApiKey))
            {
                WriteLine("[ERROR] Hugging Face API key is required. Exiting program.");
                return;
            }

            // Register AIAnalysisService with Hugging Face API key
            services.AddTransient<IAIAnalysisService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                return new AIAnalysisService(factory, huggingFaceApiKey);
            });

            // Register ArticleBodyService with its dependencies (HttpClientFactory + PlaywrightRenderService)
            services.AddTransient<ArticleBodyService>();

            // Build service and ensure DisposeAsync() is called for PlaywrightRenderService
            await using var serviceProvider = services.BuildServiceProvider();

            if (!string.Equals(apiKey, "skip", StringComparison.OrdinalIgnoreCase))
            {
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
                    WriteLine("[ERROR] " + newsResult.ErrorMessage); // Use error message from result
                    return;
                }
            }

            // Always run this section regardless of API key

            // Get ArticleBodyService with dependencies
            var articleBodyService = serviceProvider.GetRequiredService<ArticleBodyService>();

            // Prompt for the URL
            WriteLine("Please enter the URL of the article:");
            string testUrl = ReadLine();

            if (string.IsNullOrEmpty(testUrl))
            {
                WriteLine("[ERROR] URL cannot be empty. Exiting program.");
                return; // Exit if no URL is provided
            }

            // Get the article body
            ArticleBodyResult result = await articleBodyService.GetArticleBodyAsync(testUrl);
            if (result.Success)
            {
                WriteLine("Article Body: ");
                WriteLine(result.ArticleBody);

                // AIAnalysisService
                var aiAnalysisService = serviceProvider.GetRequiredService<IAIAnalysisService>();

                // Log input length before summarization
                WriteLine($"\n[INFO] Article body length: {result.ArticleBody.Length} characters");

                // Summarize the article text
                var summary = await aiAnalysisService.SummarizeArticleAsync(result.ArticleBody);
                WriteLine("\n[AI Summary]:");
                WriteLine(summary);
            }
            else
            {
                WriteLine(result.ErrorMessage); // Use error message from result
            }
        }
    }
}