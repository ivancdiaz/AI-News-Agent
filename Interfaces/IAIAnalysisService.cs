using System;
using System.Threading.Tasks;

namespace AI.News.Agent.Services
{
    public interface IAIAnalysisService
    {
        Task<string> SummarizeArticleAsync(string articleText);
    }
}