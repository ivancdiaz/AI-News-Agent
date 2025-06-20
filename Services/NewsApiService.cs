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
        private const string ApiKeyHeaderName = "x-api-key";
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly ILogger<NewsApiService> _logger;

        // Inject HttpClient and ILogger via DI, apply headers
        public NewsApiService(
            IHttpClientFactory httpClientFactory,
            string apiKey,
            string baseUrl,
            ILogger<NewsApiService> logger)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "API key cannot be null.");
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Apply centralized headers
            foreach (var header in HttpHeadersConfig.HttpClientHeaders)
            {
                _client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Apply API-specific header
            _client.DefaultRequestHeaders.TryAddWithoutValidation(ApiKeyHeaderName, _apiKey);
        }

        public async Task<Result<List<Articles>>> FetchTopHeadlinesAsync(string country = "us", int pageSize = 5)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                return Result<List<Articles>>.Fail("Country code cannot be null or empty.");
            }

            if (pageSize <= 0)
            {
                return Result<List<Articles>>.Fail("Page size must be greater than zero.");
            }

            var url = $"{_baseUrl}?country={country}&pageSize={pageSize}";

            _logger.LogInformation(
                "Fetching top headlines from {Url}",
                url);

            try
            {
                var response = await _client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                     _logger.LogWarning(
                        "Request failed with status code {StatusCode} when accessing {Url}",
                        response.StatusCode,
                        url);

                    return Result<List<Articles>>.Fail($"Request failed: {response.StatusCode}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug(
                    "Successfully fetched news JSON (Length: {Length} chars)", 
                    responseBody.Length);

                return ParseArticlesFromJson(responseBody);
            }
            catch (HttpRequestException ex)
            {
                // Handle network-related issues
                _logger.LogError(
                    ex, 
                    "Failed to fetch news from {Url}", 
                    url);

                return Result<List<Articles>>.Fail($"Failed to fetch news: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Unexpected error occurred while fetching news from {Url}", 
                    url);

                return Result<List<Articles>>.Fail($"Unexpected error: {ex.Message}");
            }
        }

        private Result<List<Articles>> ParseArticlesFromJson(string jsonString)
        {
            try
            {
                var json = JObject.Parse(jsonString);

                var articles = new List<Articles>();

                // // Children() returns array elements; falls back to empty if missing
                var articleTokens = json["articles"]?.Children() ?? Enumerable.Empty<JToken>();
                foreach (var item in articleTokens)
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

                if (articles.Count == 0)
                {
                    _logger.LogInformation("No articles found in the response JSON.");
                    return Result<List<Articles>>.Fail("No articles found.");
                }

                return Result<List<Articles>>.Ok(articles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse articles JSON.");

                return Result<List<Articles>>.Fail($"Failed to parse articles: {ex.Message}");
            }
        }
    }
}