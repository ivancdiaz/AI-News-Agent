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
        }
    }
}