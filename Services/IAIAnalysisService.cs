using System;
using System.Threading.Tasks;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public interface IAIAnalysisService
    {
        Task<Result<Summary>> SummarizeArticleAsync(string articleText);
    }
}