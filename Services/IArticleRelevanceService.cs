using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public interface IArticleRelevanceService
    {
        // Returns one ArticleRelevance per article, same order and count as articles.
        Task<Result<List<ArticleRelevance>>> EvaluateRelevanceAsync(string originalQuery, List<Articles> articles);
    }
}
