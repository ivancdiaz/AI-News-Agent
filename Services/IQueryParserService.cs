using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public interface IQueryParserService
    {
        Task<Result<ArticleSearchQuery>> ParseQuery(string userQuery);
    }
}
