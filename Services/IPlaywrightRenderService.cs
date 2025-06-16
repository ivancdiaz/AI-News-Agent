using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public interface IPlaywrightRenderService : IAsyncDisposable
    {
        Task<Result<string>> RenderPageHtmlAsync(string url);
    }
}