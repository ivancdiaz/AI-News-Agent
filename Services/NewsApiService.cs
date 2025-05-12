using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AI.News.Agent.Models;
using Newtonsoft.Json.Linq;
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
            // DNS Pre-resolution
            try
            {
                var hostEntry = await System.Net.Dns.GetHostEntryAsync("newsapi.org");
                WriteLine("[INFO] DNS resolved: " + string.Join(", ", hostEntry.AddressList.Select(ip => ip.ToString())));
            }
            catch (Exception ex)
            {
                WriteLine("[ERROR] DNS resolution failed: " + ex.Message);
                return new List<Articles>();
            }

            using var client = new HttpClient();
            var url = $"{_baseUrl}?country={country}&pageSize={pageSize}";

            // Add the API key to the headers
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

            // Set User-Agent
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AI.News.Agent/1.0");

            int maxRetries = 3;
            int delayMilliseconds = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    WriteLine($"[INFO] Attempt {attempt}: Fetching news from {url}");
                    var response = await client.GetAsync(url);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        WriteLine($"[ERROR] Attempt {attempt} failed: Status {response.StatusCode} - {responseBody}");
                        if (attempt == maxRetries)
                        {
                            WriteLine("[ERROR] Max retries reached. Giving up.");
                            return new List<Articles>();
                        }
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2; // Exponential backoff
                        continue;
                    }

                    var json = JObject.Parse(responseBody);
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
                    WriteLine($"[ERROR] Attempt {attempt} failed with exception: {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        WriteLine("[ERROR] Max retries reached. Giving up.");
                        return new List<Articles>();
                    }
                    await Task.Delay(delayMilliseconds);
                    delayMilliseconds *= 2;
                }
            }
            return new List<Articles>();
        }
    }
}