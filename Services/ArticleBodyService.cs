using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using AI.News.Agent.Config;
using static System.Console;

namespace AI.News.Agent.Services
{
    public class ArticleBodyService
    {
        private readonly HttpClient _client;
        private readonly IPlaywrightRenderService _playwrightService;

        // Inject HttpClient and Playwright render service
        // Apply centralized default headers to the HttpClient instance
        public ArticleBodyService(IHttpClientFactory httpClientFactory, IPlaywrightRenderService playwrightService)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient"); // Inject HttpClient
            _playwrightService = playwrightService; // Inject playwright

            // Centralized User-Agent and headers
            foreach (var header in HttpHeadersConfig.HttpClientHeaders)
            {
                if (!_client.DefaultRequestHeaders.Contains(header.Key))
                {
                    _client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }

        public async Task<ArticleBodyResult> GetArticleBodyAsync(string url)
        {
            string? html = null;
            
            // Attempt to fetch the raw HTML using HttpClient
            try
            {
                html = await _client.GetStringAsync(url);

                // Try parsing raw HTML
                var result = TryParseHtml(html);
                if (result.Success)
                {
                    WriteLine("[INFO] Successfully parsed article using HtmlAgilityPack (HTML fetched via HttpClient).");
                    return result;
                }

                WriteLine("[WARN] HttpClient fetch succeeded, but parsing returned no usable content.");
            }
            catch (HttpRequestException ex)
            {
                WriteLine($"[WARN] HttpClient failed: {ex.Message}");
            }
            
            // Fallback: use Playwright to render the page and capture HTML if HttpClient fails or content was unusable
            html = await _playwrightService.RenderPageHtmlAsync(url);
            if (html == null)
            {
                return new ArticleBodyResult
                {
                    ErrorMessage = "[ERROR] Both HttpClient and Playwright failed to fetch usable article HTML."
                };
            }

            // Try parsing the fallback HTML
            var fallbackResult = TryParseHtml(html);
            if (fallbackResult.Success)
            {
                WriteLine("[INFO] Successfully parsed article using HtmlAgilityPack (HTML fetched via Playwright).");
                return fallbackResult;
            }

            // Parsing failed for both HttpClient and Playwright
            return new ArticleBodyResult
            {
                ErrorMessage = "[ERROR] Playwright fetch succeeded, but parsing returned no usable content."
            };
        }

        // Combined parsing logic
        private ArticleBodyResult TryParseHtml(string html)
        {
            var result = new ArticleBodyResult();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var bodyNode = GetMainContentNode(doc);

            if (bodyNode != null)
            {
                var paragraphs = bodyNode.SelectNodes(".//p");
                if (paragraphs != null && paragraphs.Count > 0)
                {
                    var articleText = string.Join("\n\n", paragraphs.Select(p => p.InnerText.Trim()));
                    result.ArticleBody = CleanText(articleText);
                    return result;
                }
            }
            result.ErrorMessage = "[INFO] Article body not found or had no meaningful paragraph content.";
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

            // Final fallback: Find the <div> with the most paragraph text.
            if (bodyNode == null)
            {
                var divs = doc.DocumentNode.SelectNodes("//div[count(.//p) >= 2]");
                if (divs != null)
                {
                    bodyNode = divs
                        .OrderByDescending(div =>
                            div.SelectNodes(".//p")?.Sum(p => p.InnerText.Length) ?? 0)
                        .FirstOrDefault();

                    if (bodyNode != null)
                    {
                        Console.WriteLine("[INFO] Fallback: Found div with most paragraph content.");
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