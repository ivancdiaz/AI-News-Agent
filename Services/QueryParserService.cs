using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AI.News.Agent.Config;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    // Real IQueryParserService using Qwen3-32B via Hugging Face Inference Providers.
    // Fail-fast: never falls back to MockQueryParserService.
    public class QueryParserService : IQueryParserService
    {
        // Values NewsAPI /everything accepts. Anything else from the model is rejected.
        private static readonly HashSet<string> AllowedLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar", "de", "en", "es", "fr", "he", "it", "nl", "no", "pt", "ru", "sv", "ud", "zh"
        };

        // Maps any casing from the model to the exact casing NewsAPI expects
        private static readonly Dictionary<string, string> AllowedSortBy = new(StringComparer.OrdinalIgnoreCase)
        {
            ["relevancy"] = "relevancy",
            ["popularity"] = "popularity",
            ["publishedAt"] = "publishedAt"
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<QueryParserService> _logger;
        private readonly QueryParserSettings _settings; // single source for defaults and limits
        private readonly string _endpointUrl;
        private readonly string _model;

        // Reuses the existing Hugging Face token; it needs the "Inference Providers" permission
        public QueryParserService(
            IHttpClientFactory httpClientFactory,
            string huggingFaceApiKey,
            ILogger<QueryParserService> logger,
            QueryParserSettings settings)
        {
            if (huggingFaceApiKey == null) throw new ArgumentNullException(nameof(huggingFaceApiKey));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _endpointUrl = settings.Url ?? throw new ArgumentNullException(nameof(settings.Url));
            _model = settings.Model ?? throw new ArgumentNullException(nameof(settings.Model));

            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", huggingFaceApiKey);
        }

        public async Task<Result<ArticleSearchQuery>> ParseQuery(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
            {
                _logger.LogWarning("ParseQuery called with null or empty query.");
                return Result<ArticleSearchQuery>.Fail("Search query text cannot be empty.");
            }

            var today = DateTime.UtcNow.Date;

            var payload = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = BuildSystemPrompt(today) },
                    // "/no_think" is Qwen3's soft switch to skip the <think> reasoning block
                    new { role = "user", content = $"{userQuery.Trim()} /no_think" }
                },
                temperature = 0,
                max_tokens = 512,
                response_format = new { type = "json_object" }
            };

            _logger.LogInformation(
                "Sending query to {Model} for parsing: {UserQuery}",
                _model,
                userQuery);

            try
            {
                using var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_endpointUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Query parser request failed with status {StatusCode}: {ResponseBody}",
                        response.StatusCode,
                        responseBody);

                    return Result<ArticleSearchQuery>.Fail($"Query parser request failed with status code {(int)response.StatusCode}.");
                }

                var modelText = JObject.Parse(responseBody)["choices"]?[0]?["message"]?["content"]?.ToString();
                if (string.IsNullOrWhiteSpace(modelText))
                {
                    _logger.LogError("Query parser response contained no message content.");
                    return Result<ArticleSearchQuery>.Fail("Query parser returned an empty response.");
                }

                _logger.LogDebug(
                    "Raw query parser output: {ModelText}",
                    modelText);

                var json = ExtractJson(modelText);
                if (json == null)
                {
                    _logger.LogError(
                        "No JSON object found in query parser output: {ModelText}",
                        modelText);

                    return Result<ArticleSearchQuery>.Fail("Query parser did not return a JSON object.");
                }

                var parsed = JsonConvert.DeserializeObject<QueryParserResponse>(json);
                if (parsed == null)
                {
                    return Result<ArticleSearchQuery>.Fail("Query parser returned an unreadable JSON object.");
                }

                return ValidateAndMap(parsed, today);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to read query parser JSON.");
                return Result<ArticleSearchQuery>.Fail("Query parser returned invalid JSON.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Query parser request timed out.");
                return Result<ArticleSearchQuery>.Fail("Query parser request timed out.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while calling query parser.");
                return Result<ArticleSearchQuery>.Fail($"Query parser request failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while parsing query.");
                return Result<ArticleSearchQuery>.Fail($"Query parsing failed: {ex.Message}");
            }
        }

        // Model output is untrusted (it ends up in the NewsAPI URL), so every field is validated here:
        // unknown language/sortBy/date fail, missing optional fields get defaults, ranges are clamped.
        private Result<ArticleSearchQuery> ValidateAndMap(QueryParserResponse parsed, DateTime today)
        {
            var q = parsed.Q?.Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                return Result<ArticleSearchQuery>.Fail("Query parser returned no search terms.");
            }

            if (q.Length > _settings.MaxQueryLength)
            {
                return Result<ArticleSearchQuery>.Fail($"Query parser returned search terms longer than {_settings.MaxQueryLength} characters.");
            }

            var language = _settings.DefaultLanguage;
            if (!string.IsNullOrWhiteSpace(parsed.Language))
            {
                var candidate = parsed.Language.Trim();
                if (!AllowedLanguages.Contains(candidate))
                {
                    _logger.LogError(
                        "Query parser returned unsupported language: {Language}",
                        candidate);

                    return Result<ArticleSearchQuery>.Fail("Query parser returned an unsupported language.");
                }
                language = candidate.ToLowerInvariant();
            }

            var sortBy = _settings.DefaultSortBy;
            if (!string.IsNullOrWhiteSpace(parsed.SortBy))
            {
                var candidate = parsed.SortBy.Trim();
                if (!AllowedSortBy.TryGetValue(candidate, out var canonicalSortBy))
                {
                    _logger.LogError(
                        "Query parser returned unsupported sortBy: {SortBy}",
                        candidate);

                    return Result<ArticleSearchQuery>.Fail("Query parser returned an unsupported sort order.");
                }
                sortBy = canonicalSortBy;
            }

            var from = today.AddDays(-_settings.DefaultLookbackDays);
            if (!string.IsNullOrWhiteSpace(parsed.From))
            {
                if (!DateTime.TryParse(
                        parsed.From,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var parsedFrom))
                {
                    _logger.LogError(
                        "Query parser returned unreadable date: {From}",
                        parsed.From);

                    return Result<ArticleSearchQuery>.Fail("Query parser returned an unreadable date.");
                }

                // Keep the date inside the range NewsAPI can serve
                var earliest = today.AddDays(-_settings.MaxLookbackDays);
                from = parsedFrom.Date < earliest ? earliest
                     : parsedFrom.Date > today ? today
                     : parsedFrom.Date;
            }

            var query = new ArticleSearchQuery
            {
                Query = q,
                From = from,
                Language = language,
                SortBy = sortBy
            };

            _logger.LogInformation(
                "Query parser mapped input to ArticleSearchQuery (Query: {Query}, From: {From}, Language: {Language}, SortBy: {SortBy})",
                query.Query, query.From, query.Language, query.SortBy);

            return Result<ArticleSearchQuery>.Ok(query);
        }

        // Qwen3 may prefix its answer with a <think>...</think> block. Drop it, then take the outermost {...}.
        private static string? ExtractJson(string text)
        {
            var withoutThinking = Regex.Replace(
                text,
                @"<think>.*?</think>",
                string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();

            var start = withoutThinking.IndexOf('{');
            var end = withoutThinking.LastIndexOf('}');

            return start >= 0 && end > start
                ? withoutThinking.Substring(start, end - start + 1)
                : null;
        }

        // Today's date is injected so the model can resolve phrases like "recent" or "last week"
        private string BuildSystemPrompt(DateTime today)
        {
            var todayText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var earliestText = today.AddDays(-_settings.MaxLookbackDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var exampleFromText = today.AddDays(-_settings.DefaultLookbackDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return string.Join("\n", new[]
            {
                "You convert a user's natural-language news request into search parameters for the NewsAPI /v2/everything endpoint.",
                $"Today's date is {todayText} (UTC).",
                "Respond with ONLY a single JSON object (no prose, no markdown) using these fields:",
                "- \"q\" (string, required): concise search keywords without filler words such as \"find\", \"recent\" or \"news\". Quoted phrases and AND/OR/NOT are allowed.",
                "- \"language\" (string, optional): a two-letter code from: ar, de, en, es, fr, he, it, nl, no, pt, ru, sv, ud, zh. Omit it unless the user asks for a language.",
                "- \"sortBy\" (string, optional): one of \"relevancy\", \"popularity\", \"publishedAt\". Omit it unless the user implies an ordering.",
                $"- \"from\" (string, optional): earliest publication date as YYYY-MM-DD, never earlier than {earliestText}. Use it to resolve phrases like \"recent\" (last {_settings.DefaultLookbackDays} days), \"today\" or \"last week\". Omit it if the user gives no time frame.",
                $"Example: {{\"q\":\"solar panel tariffs\",\"sortBy\":\"publishedAt\",\"from\":\"{exampleFromText}\"}}"
            });
        }

        // Shape of the JSON the model is asked to produce. Kept private so ArticleSearchQuery stays untouched.
        private sealed class QueryParserResponse
        {
            [JsonProperty("q")] public string? Q { get; set; }
            [JsonProperty("language")] public string? Language { get; set; }
            [JsonProperty("sortBy")] public string? SortBy { get; set; }
            [JsonProperty("from")] public string? From { get; set; }
        }
    }
}
