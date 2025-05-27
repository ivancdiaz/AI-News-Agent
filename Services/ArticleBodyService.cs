using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using AI.News.Agent.Config;

namespace AI.News.Agent.Services
{
    public class ArticleBodyService
    {
        private readonly HttpClient _client;

        // Inject HttpClient via DI and set centralized default headers once
        public ArticleBodyService(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");

            // Centralized User-Agent and headers
            foreach (var header in HttpHeadersConfig.DefaultHeaders)
            {
                if (!_client.DefaultRequestHeaders.Contains(header.Key))
                {
                    _client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }

        public async Task<ArticleBodyResult> GetArticleBodyAsync(string url)
        {
            var result = new ArticleBodyResult();

            try
            {
                var html = await _client.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Fetch article body using <article> or a div with the class "article-body"
                var bodyNode = GetMainContentNode(doc);

                if (bodyNode != null)
                {
                    // Extract meaningful content using <p> tags to reduce noise
                    var paragraphs = bodyNode.SelectNodes(".//p");
                    if (paragraphs != null && paragraphs.Count > 0)
                    {
                        var articleText = string.Join("\n\n", paragraphs.Select(p => p.InnerText.Trim()));
                        result.ArticleBody = CleanText(articleText);
                    }
                    else
                    {
                        result.ErrorMessage = "[INFO] No paragraph content found inside article body.";
                    }
                }
                else
                {
                    result.ErrorMessage = "[INFO] Article body not found on the page.";
                }
            }
            catch (HttpRequestException ex)
            {
                result.ErrorMessage = $"[ERROR] Failed to fetch article: {ex.Message}";
            }
            return result;
        }

        // Locate the main content in the HTML document
        // Use <article> and common article divs first
        // Then fall back to semantic containers with multiple <p> tags
        private HtmlNode? GetMainContentNode(HtmlDocument doc)
        {
            // Fetch article body using <article> or a div with the class "article-body"
            var bodyNode = doc.DocumentNode.SelectSingleNode("//article")
                         ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-body')]");

            // If default selectors fail use common semantic containers
            if (bodyNode == null)
            {
                var articleBodyXPaths = new[]
                {
                    "//div[contains(@class, 'story-body') or contains(@id, 'story-body')]",
                    "//div[contains(@class, 'entry-content') or contains(@id, 'entry-content')]",
                    "//div[contains(@class, 'post-content') or contains(@id, 'post-content')]",
                    "//section[contains(@class, 'article') or contains(@id, 'article')]",
                    "//div[contains(@class, 'article') or contains(@id, 'article')]",
                    "//div[contains(translate(@class, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'article')]"
                };

                foreach (var xpath in articleBodyXPaths)
                {
                    var candidateNode = doc.DocumentNode.SelectSingleNode(xpath);
                    if (candidateNode != null && candidateNode.SelectNodes(".//p")?.Count >= 2)
                    {
                        bodyNode = candidateNode;
                        break;
                    }
                }
            }
            return bodyNode;
        }

        // Cleans filters and normalizes article body text
        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = WebUtility.HtmlDecode(text);
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            // Keep skip list minimal to avoid over-trimming
            var skipKeywords = new[]
            {
                "cookie", "advertisement", "sign up", "privacy policy",
                "terms of use", "get the app"
            };

            lines = lines
                .Where(line =>
                    !(line.Length < 40 && line.All(c => char.IsUpper(c) || !char.IsLetter(c))) &&
                    !skipKeywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase))
                )
                .ToList();

            // Join cleaned lines into paragraphs
            var cleaned = string.Join("\n\n", lines);
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");              // collapse multiple spaces
            cleaned = Regex.Replace(cleaned, @"(\r?\n\s*){2,}", "\n\n");   // normalize paragraph breaks

            return cleaned.Trim();
        }
    }

    // Result status
    public class ArticleBodyResult
    {
        public string? ArticleBody { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage); // Determines if the operation was successful
    }
}