using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public class AIAnalysisService : IAIAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string _huggingFaceApiKey;
        private readonly ILogger<AIAnalysisService> _logger;
        private readonly string _summarizationModelUrl = "https://api-inference.huggingface.co/models/facebook/bart-large-cnn";

        // Constants for controlling chunking and token estimation
        private const int MaxTokensPerChunk = 900; // Set to 900 to stay 10–15% below HF limit due to token size estimation variance
        private const int EstimatedCharsPerToken = 4; // Manual estimate; future fix: add tokenizer for more precise token counts
        private const int FinalSummaryTokenBudget = 300;
        private const int MaxQuickSummaryTokenBudget = 200; // Smaller budget for short articles to prevent AI overgeneration
        private const int MinSummaryTokenLength = 50;

        // Minimum lengths as % of token budgets for chunk and final summaries
        private const double ChunkSummaryMinLengthPercentage = 0.8; 
        private const double FinalSummaryMinLengthPercentage = 0.8;

        // Using DI to inject IHttpClientFactory and API key for HTTP setup
        public AIAnalysisService(
            IHttpClientFactory httpClientFactory,
            string huggingFaceApiKey,
            ILogger<AIAnalysisService> logger)
        {
            _huggingFaceApiKey = huggingFaceApiKey ?? throw new ArgumentNullException(nameof(huggingFaceApiKey));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _huggingFaceApiKey);
        }

        public async Task<Result<Summary>> SummarizeArticleAsync(string articleText)
        {
            if (string.IsNullOrWhiteSpace(articleText))
            {
                _logger.LogWarning("Article text is empty or null.");
                return Result<Summary>.Fail("Article text is empty.");
            }

            var cleanedArticle = CleanText(articleText);

            // Estimate total tokens in the article based on character length
            int estimatedTotalTokens = cleanedArticle.Length / EstimatedCharsPerToken;

            // Early return for very short articles
            if (estimatedTotalTokens < MinSummaryTokenLength)
            {
                _logger.LogInformation("Article very short, returning cleaned text without summarization.");
                return Result<Summary>.Ok(new Summary { Text = cleanedArticle });
            }

            // Determine how many chunks are needed based on max tokens per chunk
            int chunkCount = (int)Math.Ceiling(estimatedTotalTokens / (double)MaxTokensPerChunk);

            // If the article fits into one chunk, skip chunking and summarize directly
            if (chunkCount <= 1)
            {
                _logger.LogInformation("Article fits within a single chunk, skipping chunking.");

                // Dynamic token budget: scale to 25% of input tokens (min MinSummaryTokenLength, max MaxQuickSummaryTokenBudget)
                int quickSummaryTokenBudget = Math.Min(MaxQuickSummaryTokenBudget, Math.Max(MinSummaryTokenLength, estimatedTotalTokens / 4));

                var quickSummaryResult = await SummarizeTextAsync(
                    cleanedArticle,
                    quickSummaryTokenBudget,
                    FinalSummaryMinLengthPercentage);

                if (!quickSummaryResult.Success)
                {
                    _logger.LogError(
                        "Quick summary failed: {ErrorMessage}", 
                        quickSummaryResult.ErrorMessage);
                    return Result<Summary>.Fail(quickSummaryResult.ErrorMessage ?? "Unknown summarization error.");
                }

                int quickSummaryTokenCount = quickSummaryResult.Value.Text.Length / EstimatedCharsPerToken;

                _logger.LogInformation(
                    "Final summary token count: ~{QuickSummaryTokenCount}",
                    quickSummaryTokenCount);

                return Result<Summary>.Ok(quickSummaryResult.Value);
            }

            // Calculate size of each chunk in characters
            int chunkSizeInChars = (int)Math.Ceiling(cleanedArticle.Length / (double)chunkCount);

            // Split article into chunks
            var chunks = ChunkText(cleanedArticle, chunkSizeInChars);
            var chunkSummaries = new List<string>();

            // Distribute a budget of tokens per chunk summary
            // Keeps total tokens of combined summaries under (MaxTokensPerChunk) for the final summary
            int tokenBudgetPerChunkSummary = (int)Math.Floor(MaxTokensPerChunk / (double)chunkCount);
            int chunkIndex = 1;

            foreach (var chunk in chunks)
            {
                int chunkTokenCount = chunk.Length / EstimatedCharsPerToken;
                _logger.LogInformation(
                    "Summarizing chunk #{ChunkIndex} (Length: {ChunkLength} chars, ~{ChunkTokenCount} tokens)",
                    chunkIndex,
                    chunk.Length,
                    chunkTokenCount);
                _logger.LogInformation(
                    "Assigned summary token budget: {TokenBudgetPerChunkSummary} tokens",
                    tokenBudgetPerChunkSummary);

                var chunkSummaryResult = await SummarizeTextAsync(
                    chunk,
                    tokenBudgetPerChunkSummary,
                    ChunkSummaryMinLengthPercentage);

                if (!chunkSummaryResult.Success)
                {
                    _logger.LogError(
                        "Chunk summary failed (chunk #{ChunkIndex}): {ErrorMessage}", 
                        chunkIndex, 
                        chunkSummaryResult.ErrorMessage);

                    return Result<Summary>.Fail(chunkSummaryResult.ErrorMessage ?? "Unknown chunk summarization error.");
                }

                _logger.LogInformation(
                    "\nChunk #{ChunkIndex} Summary:\n{ChunkSummary}\n",
                    chunkIndex,
                    chunkSummaryResult.Value.Text);

                chunkSummaries.Add(chunkSummaryResult.Value.Text);
                chunkIndex++;
            }

            // Combine chunk summaries for final summarization
            var combinedSummaries = string.Join(" ", chunkSummaries);
            _logger.LogInformation("Combining chunk summaries into final summary...");

            // Summarize text and return as a Summary object
            var finalSummaryResult = await SummarizeTextAsync(
                combinedSummaries,
                FinalSummaryTokenBudget,
                FinalSummaryMinLengthPercentage);

            if (!finalSummaryResult.Success)
            {
                _logger.LogError(
                    "Final summary failed: {ErrorMessage}", 
                    finalSummaryResult.ErrorMessage);
                return Result<Summary>.Fail(finalSummaryResult.ErrorMessage ?? "Unknown summarization error.");
            }

            int finalSummaryTokenCount = finalSummaryResult.Value.Text.Length / EstimatedCharsPerToken;

            _logger.LogInformation(
                "Final summary token count: ~{FinalSummaryTokenCount}",
                finalSummaryTokenCount);

            return Result<Summary>.Ok(finalSummaryResult.Value);
        }

        // Summarization call to the HuggingFace API
        private async Task<Result<Summary>> SummarizeTextAsync(string text, int maxTokenLength, double minLengthPercentage)
        {
            var cleanedText = CleanText(text);
            int maxLength = maxTokenLength;
            int minLength = Math.Max(MinSummaryTokenLength, (int)(maxLength * minLengthPercentage));

            // API request payload
            var payload = new
            {
                inputs = cleanedText,
                parameters = new
                {
                    min_length = minLength,
                    max_length = maxLength,
                    length_penalty = 0.8,
                    early_stopping = false,
                    no_repeat_ngram_size = 3
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            _logger.LogDebug(
                "Sending payload of length {JsonLength} characters.",
                json.Length);

            int payloadTokenCount = json.Length / EstimatedCharsPerToken;
            _logger.LogDebug(
                "Estimated payload token count: {PayloadTokenCount}",
                payloadTokenCount);

            const int maxRetries = 3; // Retry attempts 
            var rng = new Random();   // Create jitter for backoff delays

            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Create this instance again to avoid potential reuse bugs
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _logger.LogInformation(
                        "Waiting for summarization response (attempt {Attempt}, timeout: {TimeoutSeconds}s)...",
                        attempt, 
                        _httpClient.Timeout.TotalSeconds);

                    var response = await _httpClient.PostAsync(_summarizationModelUrl, content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var summaryResults = JsonConvert.DeserializeObject<List<Summary>>(responseBody);
                        if (summaryResults == null || summaryResults.Count == 0 || string.IsNullOrWhiteSpace(summaryResults[0]?.Text))
                        {
                            throw new Exception("Invalid summary response from API.");
                        }

                        int actualSummaryTokenCount = summaryResults[0].Text.Length / EstimatedCharsPerToken;

                        _logger.LogInformation(
                            "Actual summary token count: ~{ActualSummaryTokenCount}",
                            actualSummaryTokenCount);

                        return Result<Summary>.Ok(summaryResults[0]);
                    }

                    if ((int)response.StatusCode == 429 || ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600))
                    {
                        _logger.LogWarning(
                            "Transient HTTP error (status {StatusCode}) on attempt {Attempt}. Retrying...",
                            response.StatusCode, 
                            attempt);
                    }
                    else
                    {
                        _logger.LogError(
                            "Response Body (non-success): {ResponseBody}",
                            responseBody);

                        return Result<Summary>.Fail($"Non-success status code {response.StatusCode}: {responseBody}");
                    }
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "Summarization request timed out (attempt {Attempt}).",
                        attempt);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "Network error during summarization (attempt {Attempt}). Retrying...",
                        attempt);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "Unhandled error during summarization (attempt {Attempt}).",
                        attempt);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError(
                            ex,
                            "Final summarization failure after {MaxRetries} attempts.",
                            maxRetries);

                        return Result<Summary>.Fail($"Summarization failed: {ex.Message}");
                    }
                }

                // Exponential backoff + jitter
                int delayMs = (int)(1500 * Math.Pow(2, attempt - 1)) + rng.Next(100, 500);

                _logger.LogInformation(
                    "Delaying {DelayMs}ms before retry attempt {Attempt}...", 
                    delayMs, 
                    attempt);

                await Task.Delay(delayMs);
            }

            // Final fallback after retries
            _logger.LogError(lastException, "Summarization failed after {MaxRetries} retries.", maxRetries);
            return Result<Summary>.Fail("Summarization failed after all retries.");
        }

        // Splits the input text into chunks of specified size
        // Avoids Hugging Face Model token limit of 1024
        private List<string> ChunkText(string text, int chunkSizeInChars)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSizeInChars)
            {
                int length = Math.Min(chunkSizeInChars, text.Length - i);
                chunks.Add(text.Substring(i, length));
            }
            return chunks;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var cleaned = text
                .Replace('“', '"')
                .Replace('”', '"')
                .Replace('‘', '\'')
                .Replace('’', '\'')
                .Replace("�", "")
                .Replace("\u00A0", " ")
                .Replace("\u2013", "-")
                .Replace("\u2014", "-")
                .Replace("\u2026", "...")
                .Replace("\t", " ")
                .Replace("\r", "")
                .Replace("\n", " ");

            return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        }
    }
}