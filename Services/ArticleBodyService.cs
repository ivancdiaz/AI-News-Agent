using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AI.News.Agent.Services
{
    public class ArticleBodyService
    {
        private readonly HttpClient _client;

        // Inject IHttpClientFactory
        public ArticleBodyService(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("MyHttpClient");
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
                var bodyNode = doc.DocumentNode.SelectSingleNode("//article")
                             ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-body')]");

                if (bodyNode != null)
                {
                    result.ArticleBody = bodyNode.InnerText.Trim();
                }
                else
                {
                    result.ErrorMessage = "[INFO] Article body not found on the page."; // Retained your message style
                }
            }
            catch (HttpRequestException ex)
            {
                result.ErrorMessage = $"[ERROR] Failed to fetch article: {ex.Message}"; // Retained your message style
            }
            return result;
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