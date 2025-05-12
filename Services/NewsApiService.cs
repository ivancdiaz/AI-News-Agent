using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AI.News.Agent.Models;
using Newtonsoft.Json.Linq; // JObject.Parse
using static System.Console;

namespace AI.News.Agent.Services
{
    public class NewsApiService
    {
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://newsapi.org/v2/top-headlines";

        public NewsApiService(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");
        }

        public async Task<List<Articles>> FetchTopHeadlinesAsync(string country = "us", int pageSize = 5)
        {
            using var client = new HttpClient();
            var url = $"{_baseUrl}?country={country}&pageSize={pageSize}";

            // Add the API key to the headers
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            try
            {
                var response = await client.GetStringAsync(url);
                var json = JObject.Parse(response);
                var articles = new List<Articles>();

                foreach (var item in json["articles"]!)
                {
                    articles.Add(new Articles
                    {
                        Title = item["title"]?.ToString(),
                        Author = item["author"]?.ToString(),
                        Source = item["source"]?["name"]?.ToString(),
                        PublishedAt = DateTime.TryParse(item["publishedAt"]?.ToString(), out var date) ? date : DateTime.MinValue,
                        Description = item["description"]?.ToString(),
                        SourceUrl = item["url"]?.ToString()
                    });
                }
                return articles;
            }
            catch (Exception ex)
            {
                WriteLine("[ERROR] Error fetching news: " + ex.Message);
                return new List<Articles>();
            }
        }
    }
}