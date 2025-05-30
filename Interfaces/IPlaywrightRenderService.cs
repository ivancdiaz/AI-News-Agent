using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

public interface IPlaywrightRenderService : IAsyncDisposable
{
    Task<string?> RenderPageHtmlAsync(string url);
}