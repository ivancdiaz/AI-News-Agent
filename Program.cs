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
            // Temp: Testing URL input for article body extraction
            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddHttpClient(); // Register IHttpClientFactory
            var serviceProvider = services.BuildServiceProvider();

            // IHttpClientFactory instance
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
                WriteLine("[ERROR] " + result.ErrorMessage);
            }

            /*
            // Temp: Placeholder for NewsAPI integration
            // Prompt for API key
            WriteLine("Please enter your NewsAPI key:");
            var apiKey = ReadLine();

            if (string.IsNullOrEmpty(apiKey))
            {
                WriteLine("[ERROR] API key is required. Exiting program.");
                return; // Exit if API key is not provided
            }
            var newsService = new NewsApiService(apiKey); // Pass the API key to the service
            var outputService = new OutputService();
            var articles = await newsService.FetchTopHeadlinesAsync();
            outputService.DisplayArticles(articles);
            */
        }
    }
}