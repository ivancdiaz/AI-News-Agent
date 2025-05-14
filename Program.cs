using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using AI.News.Agent.Services;
using AI.News.Agent.Output;
using System.Net.Http;
using static System.Console;

namespace AI.News.Agent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Prompt for the API key BEFORE building the DI container
            WriteLine("Please enter your NewsAPI key:");
            var apiKey = ReadLine();

            if (string.IsNullOrEmpty(apiKey))
            {
                WriteLine("[ERROR] API key is required. Exiting program.");
                return; // Exit if no API is provided
            }

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient(); // Register IHttpClientFactory

            // Register NewsApiService with captured API key
            services.AddTransient<NewsApiService>(provider =>
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                return new NewsApiService(factory, apiKey);
            });

            // Register NewsService which depends on NewsApiService
            services.AddTransient<NewsService>();

            var serviceProvider = services.BuildServiceProvider();

            // Retrieve NewsApiService from DI
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

            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            // Create service using IHttpClientFactory
            var articleBodyService = new ArticleBodyService(httpClientFactory);

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
            }
            else
            {
                WriteLine(result.ErrorMessage); // Use error message from result
            }
        }
    }
}