using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AI.News.Agent.Services;
using AI.News.Agent.Models;

namespace AI.News.Agent.Controllers
{
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

        // GET: /api/articles/top-headlines
        [HttpGet("top-headlines")]
        [ProducesResponseType(200, Type = typeof(List<Articles>))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetTopHeadlines([FromQuery] string country = "us", [FromQuery] int pageSize = 5)
        {
            var result = await _newsApiService.FetchTopHeadlinesAsync(country, pageSize);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to fetch headlines: {Message}", 
                    result.ErrorMessage);

                return BadRequest(result.ErrorMessage);
            }

            return Ok(result.Value);
        }

        // GET: /api/articles/body
        [HttpGet("body")]
        [ProducesResponseType(200, Type = typeof(ArticleBody))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetArticleBody([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("GetArticleBody called with null or empty URL.");
                return BadRequest("URL must be provided.");
            }

            var result = await _articleBodyService.GetArticleBodyAsync(url);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to extract article body: {Message}", 
                    result.ErrorMessage);

                return BadRequest(result.ErrorMessage);
            }

            return Ok(result.Value);
        }

        // GET: /api/articles/summarize
        [HttpGet("summarize")]
        [ProducesResponseType(200, Type = typeof(Summary))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Summarize([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("Summarize called with null or empty URL.");
                return BadRequest("URL must be provided.");
            }

            var bodyResult = await _articleBodyService.GetArticleBodyAsync(url);
            if (!bodyResult.Success)
            {
                _logger.LogWarning(
                    "Body extraction failed during summarization: {Message}", 
                    bodyResult.ErrorMessage);

                return BadRequest($"Body extraction failed: {bodyResult.ErrorMessage}");
            }

            var summaryResult = await _aiAnalysisService.SummarizeArticleAsync(bodyResult.Value!.Text);
            if (!summaryResult.Success)
            {
                _logger.LogWarning(
                    "Summarization failed: {Message}", 
                    summaryResult.ErrorMessage);
                    
                return BadRequest($"Summarization failed: {summaryResult.ErrorMessage}");
            }

            return Ok(summaryResult.Value);
        }
    }
}