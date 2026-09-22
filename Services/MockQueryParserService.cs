using Microsoft.Extensions.Logging;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    // Placeholder for a future LLM-based query parser.
    // Forwards the raw input as the NewsAPI 'q' term with fixed defaults for everything else -
    // no keyword extraction, no branching. This proves the seam, not the intelligence.
    public class MockQueryParserService : IQueryParserService
    {
        private readonly ILogger<MockQueryParserService> _logger;

        public MockQueryParserService(ILogger<MockQueryParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Result<ArticleSearchQuery>> ParseQuery(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
            {
                _logger.LogWarning("ParseQuery called with null or empty query.");
                return Task.FromResult(Result<ArticleSearchQuery>.Fail("Search query text cannot be empty."));
            }

            var query = new ArticleSearchQuery
            {
                Query = userQuery.Trim(),
                From = DateTime.UtcNow.AddDays(-7),
                Language = "en",
                SortBy = "relevancy",
                PageSize = 5
            };

            _logger.LogInformation(
                "Mock query parser mapped input to ArticleSearchQuery (Query: {Query}, From: {From}, Language: {Language}, SortBy: {SortBy}, PageSize: {PageSize})",
                query.Query, query.From, query.Language, query.SortBy, query.PageSize);

            return Task.FromResult(Result<ArticleSearchQuery>.Ok(query));
        }
    }
}
