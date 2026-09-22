using System;
using System.Threading.Tasks;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public interface IArticleSummarizationService
    {
        Task<Result<Summary>> SummarizeArticleAsync(string articleText);
    }
}