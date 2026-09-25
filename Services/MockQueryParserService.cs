using Microsoft.Extensions.Logging;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    // Placeholder query parser: passes input straight through with fixed defaults, no NLP - proves the seam, not the intelligence.
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
                SortBy = "relevancy"
            };

            _logger.LogInformation(
                "Mock query parser mapped input to ArticleSearchQuery (Query: {Query}, From: {From}, Language: {Language}, SortBy: {SortBy})",
                query.Query, query.From, query.Language, query.SortBy);

            return Task.FromResult(Result<ArticleSearchQuery>.Ok(query));
        }
    }
}
