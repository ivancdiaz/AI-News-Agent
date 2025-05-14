using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AI.News.Agent.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using static System.Console;

namespace AI.News.Agent.Services
{
    public class NewsApiService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://newsapi.org/v2/top-headlines";

        // Constructor with Dependency Injection: HttpClient is created via IHttpClientFactory
        public NewsApiService(IHttpClientFactory httpClientFactory, string apiKey)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");
        }

        public async Task<List<Articles>> FetchTopHeadlinesAsync(string country = "us", int pageSize = 5)
        {
            var articles = new List<Articles>();
            var url = $"{_baseUrl}?country={country}&pageSize={pageSize}";

            // Add the API key to the request headers
            _client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            // Set User-Agent for server compatibility and logging
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("AI.News.Agent/1.0");

            try
            {
                WriteLine($"[INFO] Fetching news from {url}");
                var response = await _client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Handle HTTP errors gracefully
                    WriteLine($"[ERROR] Request failed: {response.StatusCode}");
                    return articles;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseBody);

                // Parse JSON response and map to Articles model
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
            }
            catch (HttpRequestException ex)
            {
                // Handle network-related issues
                WriteLine($"[ERROR] Failed to fetch news: {ex.Message}");
            }

            return articles;
        }
    }
}