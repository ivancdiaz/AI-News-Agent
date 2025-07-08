using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AI.News.Agent.Services;
using AI.News.Agent.Models;

namespace AI.News.Agent.Controllers
{
    /// <summary>
    /// Handles operations related to news articles, such as retrieving headlines, extracting article content, and summarizing articles.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ArticlesController : ControllerBase
    {
        private readonly NewsApiService _newsApiService;
        private readonly ArticleBodyService _articleBodyService;
        private readonly IAIAnalysisService _aiAnalysisService;
        private readonly ILogger<ArticlesController> _logger;

        public ArticlesController(
            NewsApiService newsApiService,
            ArticleBodyService articleBodyService,
            IAIAnalysisService aiAnalysisService,
            ILogger<ArticlesController> logger)
        {
            _newsApiService = newsApiService;
            _articleBodyService = articleBodyService;
            _aiAnalysisService = aiAnalysisService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the latest top headlines from a specified country.
        /// </summary>
        /// <param name="country">The country code ("us", "gb", "ca").</param>
        /// <param name="pageSize">The maximum number of articles to return (default: 5).</param>
        /// <returns>A list of top headline articles.</returns>
        [HttpGet("top-headlines")]
        [ProducesResponseType(typeof(List<Articles>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> GetTopHeadlines([FromQuery] string country = "us", [FromQuery] int pageSize = 5)
        {
            var result = await _newsApiService.FetchTopHeadlinesAsync(country, pageSize);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to fetch top headlines: {Message}",
                    result.ErrorMessage);

                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/top-headlines-fetch-failed",
                    title: "Failed to fetch news headlines",
                    detail: result.ErrorMessage!));
            }

            return Ok(result.Value);
        }

        /// <summary>
        /// Extracts the body of a news article from the given URL.
        /// </summary>
        /// <param name="url">The full URL of the article.</param>
        /// <returns>The extracted article body content.</returns>
        [HttpGet("body")]
        [ProducesResponseType(typeof(ArticleBody), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> GetArticleBody([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("GetArticleBody called with null or empty URL.");
                
                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/url-missing",
                    title: "URL is required",
                    detail: "The 'url' must be provided."));
            }

            var result = await _articleBodyService.GetArticleBodyAsync(url);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to extract article body: {Message}", 
                    result.ErrorMessage);

                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/body-extraction-failed",
                    title: "Article body extraction failed",
                    detail: result.ErrorMessage!));
            }

            return Ok(result.Value);
        }

        /// <summary>
        /// Extracts the article body from the given URL, then summarizes it using AI.
        /// </summary>
        /// <param name="url">The full URL of the article.</param>
        /// <returns>A summary of the article body content.</returns>
        [HttpGet("summarize")]
        [ProducesResponseType(typeof(Summary), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> Summarize([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("Summarize called with null or empty URL.");

                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/url-missing",
                    title: "URL is required",
                    detail: "The 'url' must be provided."));
            }

            var bodyResult = await _articleBodyService.GetArticleBodyAsync(url);
            if (!bodyResult.Success)
            {
                _logger.LogWarning(
                    "Body extraction failed during summarization: {Message}", 
                    bodyResult.ErrorMessage);

                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/body-extraction-failed",
                    title: "Article body extraction failed",
                    detail: bodyResult.ErrorMessage!));
            }

            var summaryResult = await _aiAnalysisService.SummarizeArticleAsync(bodyResult.Value!.Text);
            if (!summaryResult.Success)
            {
                _logger.LogWarning(
                    "Summarization failed: {Message}", 
                    summaryResult.ErrorMessage);

                return BadRequest(CreateProblem(
                    type: "https://ainewsagent.local/errors/summarization-failed",
                    title: "Article summarization failed",
                    detail: summaryResult.ErrorMessage!));
            }

            return Ok(summaryResult.Value);
        }

        // Sets consistent formatting for 400 level errors
        // Uses placeholder URIs (https://ainewsagent.local/errors/...) per RFC 7807 standards.
        private ProblemDetails CreateProblem(string type, string title, string detail, int status = 400)
        {
            return new ProblemDetails
            {
                Type = type,
                Title = title,
                Detail = detail,
                Status = status,
                Instance = HttpContext.Request.Path
            };
        }
    }
}