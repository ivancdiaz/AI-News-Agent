using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AI.News.Agent.Config;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    // Evaluates article relevance using TypeSafe Jev (Noul) via OpenRouter's Decisions API.
    // Enhancement, not a requirement: any failure returns Result.Fail, never throws.
    public class ArticleRelevanceService : IArticleRelevanceService
    {
        // Same true/false wording verified manually against Jev before this integration was built.
        private const string TrueCriteria = "The search result directly discusses the topic or subject the user requested.";
        private const string FalseCriteria = "The search result is unrelated, only mentions similar words, or does not meaningfully address the requested topic.";

        private readonly HttpClient _httpClient;
        private readonly ILogger<ArticleRelevanceService> _logger;
        private readonly string _endpointUrl;
        private readonly string _model;

        public ArticleRelevanceService(
            IHttpClientFactory httpClientFactory,
            string openRouterApiKey,
            ILogger<ArticleRelevanceService> logger,
            ArticleRelevanceSettings settings)
        {
            if (openRouterApiKey == null) throw new ArgumentNullException(nameof(openRouterApiKey));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _endpointUrl = settings.Url ?? throw new ArgumentNullException(nameof(settings.Url));
            _model = settings.Model ?? throw new ArgumentNullException(nameof(settings.Model));

            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouterApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(5); // Enhancement, not a requirement - fail fast rather than block the request
        }

        public async Task<Result<List<ArticleRelevance>>> EvaluateRelevanceAsync(string originalQuery, List<Articles> articles)
        {
            if (articles == null)
            {
                return Result<List<ArticleRelevance>>.Fail("Articles list cannot be null.");
            }

            if (articles.Count == 0)
            {
                return Result<List<ArticleRelevance>>.Ok(new List<ArticleRelevance>());
            }

            // Synthetic index-based record ids map answers back to articles by position.
            var recordIds = Enumerable.Range(0, articles.Count).Select(i => $"article-{i}").ToArray();

            var payload = new
            {
                model = _model,
                state = new
                {
                    userRequest = originalQuery,
                    records = articles.Select((article, i) => new
                    {
                        id = recordIds[i],
                        title = article.Title ?? string.Empty,
                        description = article.Description ?? string.Empty
                    })
                },
                questions = recordIds.ToDictionary(
                    id => QuestionId(id),
                    id => (object)new
                    {
                        type = "noul",
                        instructions = $"Is the search result identified by id \"{id}\" in state.records relevant to the userRequest in state?",
                        criteria = new { @true = TrueCriteria, @false = FalseCriteria }
                    })
            };

            _logger.LogInformation(
                "Sending {ArticleCount} articles to {Model} for batched relevance evaluation.",
                articles.Count,
                _model);

            try
            {
                using var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_endpointUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Relevance evaluation request failed with status {StatusCode}: {ResponseBody}",
                        response.StatusCode,
                        responseBody);

                    return Result<List<ArticleRelevance>>.Fail($"Relevance evaluation request failed with status code {(int)response.StatusCode}.");
                }

                var answers = JObject.Parse(responseBody)["answers"] as JObject;
                if (answers == null)
                {
                    _logger.LogError("Relevance evaluation response contained no answers object.");
                    return Result<List<ArticleRelevance>>.Fail("Relevance evaluation response contained no answers.");
                }

                // A missing answer just means a null Score for that article, not a failed batch -
                // the request-level failure path above already covers a fully failed call.
                var results = recordIds
                    .Select(id => new ArticleRelevance { Score = (double?)answers[QuestionId(id)]?["noul"] })
                    .ToList();

                _logger.LogInformation(
                    "Relevance evaluation completed for {ArticleCount} articles.",
                    results.Count);

                return Result<List<ArticleRelevance>>.Ok(results);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to read relevance evaluation JSON.");
                return Result<List<ArticleRelevance>>.Fail("Relevance evaluation returned invalid JSON.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Relevance evaluation request timed out.");
                return Result<List<ArticleRelevance>>.Fail("Relevance evaluation request timed out.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while calling relevance evaluation.");
                return Result<List<ArticleRelevance>>.Fail($"Relevance evaluation request failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during relevance evaluation.");
                return Result<List<ArticleRelevance>>.Fail($"Relevance evaluation failed: {ex.Message}");
            }
        }

        private static string QuestionId(string recordId) => $"relevant_{recordId}";
    }
}
