using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using AI.News.Agent.Config;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public class NewsApiService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://newsapi.org/v2/top-headlines";
        private readonly ILogger<NewsApiService> _logger; // 🟢


        // Inject HttpClient and ILogger via DI, apply headers 🟢
        public NewsApiService(
            IHttpClientFactory httpClientFactory,
            string apiKey,
            ILogger<NewsApiService> logger)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // 🟢

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

            _logger.LogInformation(
                "Fetching top headlines from {Url}",
                url); // 🟢

            try
            {
                var response = await _client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                     _logger.LogWarning(
                        "Request failed with status code {StatusCode} when accessing {Url}",
                        response.StatusCode,
                        url); // 🟢
                    result.ErrorMessage = $"Request failed: {response.StatusCode}";
                    return result;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Successfully fetched news JSON (Length: {Length} chars)", responseBody.Length); // 🟢

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
                _logger.LogError(ex, "Failed to fetch news from {Url}", url); // 🟢
                result.ErrorMessage = $"Failed to fetch news: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching news from {Url}", url); // 🟢
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
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