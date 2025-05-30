using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Console;

namespace AI.News.Agent.Services
{
    public class AIAnalysisService : IAIAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string _huggingFaceApiKey;
        private readonly string _summarizationModelUrl = "https://api-inference.huggingface.co/models/facebook/bart-large-cnn";

        // Constants for controlling chunking and token estimation
        private const int MaxTokensPerChunk = 1024;
        private const int EstimatedCharsPerToken = 4;
        private const int FinalSummaryTokenBudget = 400;
        private const int MaxQuickSummaryTokenBudget = 200; // Smaller budget for short articles to prevent AI overgeneration

        // Percent of max tokens that should be used as minimum for chunk/final summaries
        private const double ChunkSummaryMinLengthPercentage = 0.8; // Percentage used of the token budget
        private const double FinalSummaryMinLengthPercentage = 0.8; // Percentage used of the token budget

        // Using DI to inject IHttpClientFactory and API key for HTTP setup
        public AIAnalysisService(IHttpClientFactory httpClientFactory, string huggingFaceApiKey)
        {
            _huggingFaceApiKey = huggingFaceApiKey ?? throw new ArgumentNullException(nameof(huggingFaceApiKey));
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _huggingFaceApiKey);
        }

        public async Task<string> SummarizeArticleAsync(string articleText)
        {
            if (string.IsNullOrWhiteSpace(articleText))
                return "Error: article text is empty.";

            string cleanedArticle = CleanText(articleText);

            // Estimate total tokens in the article based on character length
            int estimatedTotalTokens = cleanedArticle.Length / EstimatedCharsPerToken;

            // Early return for very short articles
            if (estimatedTotalTokens < 50)
            {
                WriteLine("[INFO] Article very short, returning cleaned text without summarization.");
                return cleanedArticle;
            }

            // Determine how many chunks are needed based on max tokens per chunk
            int chunkCount = (int)Math.Ceiling(estimatedTotalTokens / (double)MaxTokensPerChunk);

            // If the article fits into one chunk, skip chunking and summarize directly
            if (chunkCount <= 1)
            {
                WriteLine("[INFO] Article fits within a single chunk, skipping chunking.");

                // Dynamic token budget: scale to 25% of input tokens (min 50, max 200)
                int quickSummaryTokenBudget = Math.Min(MaxQuickSummaryTokenBudget, Math.Max(50, estimatedTotalTokens / 4));

                var quickSummary = await SummarizeTextAsync(
                    cleanedArticle,
                    quickSummaryTokenBudget,
                    FinalSummaryMinLengthPercentage);

                int quickSummaryTokenCount = quickSummary.Length / EstimatedCharsPerToken;
                WriteLine($"[INFO] Final summary token count: ~{quickSummaryTokenCount}");

                return quickSummary;
            }

            // Calculate size of each chunk in characters
            int chunkSizeInChars = (int)Math.Ceiling(cleanedArticle.Length / (double)chunkCount);

            // Split article into chunks
            var chunks = ChunkText(cleanedArticle, chunkSizeInChars);
            var chunkSummaries = new List<string>();

            // Distribute a budget of tokens per chunk summary
            // Keeps total tokens of combined summaries under 1024 for the final summary
            int tokenBudgetPerChunkSummary = (int)Math.Floor(MaxTokensPerChunk / (double)chunkCount);
            int chunkIndex = 1;

            foreach (var chunk in chunks)
            {
                int chunkTokenCount = chunk.Length / EstimatedCharsPerToken;
                WriteLine($"\n[INFO] Summarizing chunk #{chunkIndex} (Length: {chunk.Length} chars, ~{chunkTokenCount} tokens)");
                WriteLine($"[INFO] Assigned summary token budget: {tokenBudgetPerChunkSummary} tokens");

                var chunkSummary = await SummarizeTextAsync(
                    chunk,
                    tokenBudgetPerChunkSummary,
                    ChunkSummaryMinLengthPercentage);

                WriteLine($"\n[INFO] Chunk #{chunkIndex} Summary:");
                WriteLine(chunkSummary);

                chunkSummaries.Add(chunkSummary);
                chunkIndex++;
            }

            // Combine chunk summaries for final summarization
            var combinedSummaries = string.Join(" ", chunkSummaries);
            WriteLine("\n[INFO] Combining chunk summaries into final summary...");

            var finalSummary = await SummarizeTextAsync(
                combinedSummaries,
                FinalSummaryTokenBudget,
                FinalSummaryMinLengthPercentage);

            int finalSummaryTokenCount = finalSummary.Length / EstimatedCharsPerToken;
            WriteLine($"[INFO] Final summary token count: ~{finalSummaryTokenCount}");

            return finalSummary;
        }

        // Summarization call to the HuggingFace API
        private async Task<string> SummarizeTextAsync(string text, int maxTokenLength, double minLengthPercentage)
        {
            try
            {
                var cleanedText = CleanText(text);

                int maxLength = maxTokenLength;
                int minLength = Math.Max(50, (int)(maxLength * minLengthPercentage));

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
                WriteLine($"[DEBUG] Sending payload of length {json.Length} characters.");
                WriteLine($"[DEBUG] Payload sample: {json.Substring(0, Math.Min(200, json.Length))}...");

                int payloadTokenCount = json.Length / EstimatedCharsPerToken;
                WriteLine($"[DEBUG] Estimated payload token count: {payloadTokenCount}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_summarizationModelUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    WriteLine($"[ERROR] Response Body: {responseBody}");
                    return $"[ERROR] Status: {response.StatusCode}, Body: {responseBody}";
                }

                var summaryResults = JsonConvert.DeserializeObject<List<SummaryResult>>(responseBody);
                if (summaryResults == null || summaryResults.Count == 0 || string.IsNullOrWhiteSpace(summaryResults[0]?.summary_text))
                {
                    return "[ERROR] Invalid summary response from API.";
                }

                var summary = summaryResults[0].summary_text;
                int actualSummaryTokenCount = summary.Length / EstimatedCharsPerToken;
                WriteLine($"[INFO] Actual summary token count: ~{actualSummaryTokenCount}");

                return summary;
            }
            catch (Exception ex)
            {
                return $"[EXCEPTION] {ex.GetType().Name}: {ex.Message}";
            }
        }

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

        // Splits the input text into chunks of specified size
        // Avoids Hugging Face Model token limit of 1024
        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var cleaned = text
                .Replace('“', '"').Replace('”', '"')
                .Replace('‘', '\'').Replace('’', '\'')
                .Replace("�", "").Replace("\u00A0", " ")
                .Replace("\u2013", "-").Replace("\u2014", "-")
                .Replace("\u2026", "...").Replace("\t", " ")
                .Replace("\r", "").Replace("\n", " ");

            return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        }

        private class SummaryResult
        {
            public string summary_text { get; set; }
        }
    }
}