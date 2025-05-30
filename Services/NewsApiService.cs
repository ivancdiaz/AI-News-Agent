using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using AI.News.Agent.Config;
using AI.News.Agent.Models;
using static System.Console;

namespace AI.News.Agent.Services
{
    public class NewsApiService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://newsapi.org/v2/top-headlines";

        // Inject HttpClient via DI and set centralized default headers once
        public NewsApiService(IHttpClientFactory httpClientFactory, string apiKey)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");

            // Apply centralized headers
            foreach (var header in HttpHeadersConfig.HttpClientHeaders)
            {
                _client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Apply API-specific header
            _client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", _apiKey);
        }

        public async Task<NewsApiResult> FetchTopHeadlinesAsync(string country = "us", int pageSize = 5)
        {
            var result = new NewsApiResult();
            var url = $"{_baseUrl}?country={country}&pageSize={pageSize}";

            try
            {
                WriteLine($"[INFO] Fetching news from {url}");
                var response = await _client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Handle HTTP errors gracefully
                    var msg = $"[ERROR] Request failed: {response.StatusCode}";
                    WriteLine(msg);
                    result.ErrorMessage = msg;
                    return result;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseBody);

                // Parse JSON response and map to Articles model
                foreach (var item in json["articles"]!)
                {
                    result.Articles.Add(new Articles
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
                var msg = $"[ERROR] Failed to fetch news: {ex.Message}";
                WriteLine(msg);
                result.ErrorMessage = msg;
            }
            return result;
        }
    }

    public class NewsApiResult
    {
        public List<Articles> Articles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }
}