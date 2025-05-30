using Microsoft.Playwright;
using AI.News.Agent.Config;
using static System.Console;

public class PlaywrightRenderService : IPlaywrightRenderService, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _isInitialized = false;

    public async Task<string?> RenderPageHtmlAsync(string url)
    {
        try
        {
            await EnsureInitializedAsync();

            if (_browser == null)
            {
                WriteLine("[Playwright Error] Browser not initialized.");
                return null;
            }

            // Centralized User-Agent and headers
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = HttpHeadersConfig.UserAgent,
                Locale = "en-US",
                ExtraHTTPHeaders = HttpHeadersConfig.PlaywrightHeaders
            });

            var page = await context.NewPageAsync();

            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 20000 // 20 seconds
            });

            await page.WaitForTimeoutAsync(2000); // wait for dynamic content to render

            var content = await page.ContentAsync();

            await context.CloseAsync();
            return content;
        }
        catch (TimeoutException ex)
        {
            WriteLine($"[Playwright Timeout] {url} → {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            WriteLine($"[Playwright Error] {url} → {ex.Message}");
            return null;
        }
    }

    // Reuse the same headless browser instance
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Timeout = 10000
        });

        _isInitialized = true;
    }

    // Dispose the browser instance and reset state to prevent reuse after disposal
    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        _isInitialized = false;
    }
}