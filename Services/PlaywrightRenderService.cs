using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using AI.News.Agent.Config;

public class PlaywrightRenderService : IPlaywrightRenderService, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _isInitialized = false;
    private readonly ILogger<PlaywrightRenderService> _logger; // 🟢

    public PlaywrightRenderService(ILogger<PlaywrightRenderService> logger) // 🟢
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // 🟢
    }

    public async Task<string?> RenderPageHtmlAsync(string url)
    {
        try
        {
            await EnsureInitializedAsync();

            if (_browser == null)
            {
                _logger.LogError(
                    "Browser instance is null. Cannot render page: {Url}",
                    url); // 🟢
                return null;
            }

            _logger.LogInformation("Creating new browser context for: {Url}", url); // 🟢

            // Centralized User-Agent and headers
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = HttpHeadersConfig.UserAgent,
                Locale = "en-US",
                ExtraHTTPHeaders = HttpHeadersConfig.PlaywrightHeaders
            });

            var page = await context.NewPageAsync();

            _logger.LogInformation("Navigating to page: {Url}", url); // 🟢

            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 20000 // 20 seconds
            });

            _logger.LogInformation("Waiting for content to render..."); // 🟢
            await page.WaitForTimeoutAsync(2000); // wait for content to render

            var content = await page.ContentAsync();
            _logger.LogDebug(
                "Page content length: {Length} characters",
                content?.Length ?? 0); // 🟢


            await context.CloseAsync();
            return content;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout while rendering page: {Url}", url); // 🟢
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while rendering page: {Url}", url); // 🟢 
            return null;
        }
    }

    // Reuse the same headless browser instance
    private async Task EnsureInitializedAsync()
    {
        // Skip initialization if already done
        if (_isInitialized)
        {
            return;
        }

        _logger.LogInformation("Initializing Playwright and launching headless browser..."); // 🟢

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Timeout = 10000
        });

        _isInitialized = true;
        _logger.LogInformation("Browser initialized successfully."); // 🟢
    }

    // Dispose the browser instance and reset state to prevent reuse after disposal
    public async ValueTask DisposeAsync()
    {
        /*
        // DisposeAsync called, but Playwright was never initialized. Skipping cleanup
        if (!_isInitialized)
        {
            return;
        }
        */

        _logger.LogInformation("Disposing Playwright and closing browser..."); // 🟢

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        _isInitialized = false;

         _logger.LogInformation("Playwright disposed."); // 🟢
    }
}