using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using static System.Console;

namespace AI.News.Agent.Services
{
    public class AIAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string _huggingFaceApiKey;
        private readonly string _summarizationModelUrl = "https://api-inference.huggingface.co/models/facebook/bart-large-cnn";

        // Hugging Face typically handles best < 1024 tokens, which is ~3000-4000 characters
        private const int MaxInputLength = 3500;

        // Accept API key via constructor
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

            // Step 1: Break long input into chunks of MaxInputLength
            var chunks = ChunkText(articleText, MaxInputLength);

            var chunkSummaries = new List<string>();

            int chunkIndex = 1;
            foreach (var chunk in chunks)
            {
                WriteLine($"\n[INFO] Summarizing chunk #{chunkIndex} (Length: {chunk.Length} chars):");
                WriteLine(chunk); // Temp: Show full chunk text

                var chunkSummary = await SummarizeTextAsync(chunk);

                WriteLine($"\n[INFO] Chunk #{chunkIndex} Summary:");
                WriteLine(chunkSummary); // Temp: Show chunk summary

                chunkSummaries.Add(chunkSummary);
                chunkIndex++;
            }

            // Step 2: Summarize the summaries to get a final summary
            var combinedSummaries = string.Join(" ", chunkSummaries);
            WriteLine("\n[INFO] Combining chunk summaries into final summary...");
            var finalSummary = await SummarizeTextAsync(combinedSummaries);

            return finalSummary;
        }


        // Sends text to HuggingFace for summarization.
        private async Task<string> SummarizeTextAsync(string text)
        {
            try
            {
                var payload = new { inputs = text };
                var json = JsonConvert.SerializeObject(payload);
                WriteLine($"[DEBUG] Sending payload of length {json.Length} characters.");
                WriteLine($"[DEBUG] Payload sample: {json.Substring(0, Math.Min(200, json.Length))}...");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_summarizationModelUrl, content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    WriteLine($"[ERROR] Response Body: {responseBody}");
                    return $"[ERROR] Status: {response.StatusCode}, Body: {responseBody}";
                }

                var parsed = JsonConvert.DeserializeObject<List<SummaryResult>>(responseBody);
                return parsed?[0]?.summary_text ?? "[ERROR] Summary not found in response.";
            }
            catch (Exception ex)
            {
                return $"[EXCEPTION] {ex.GetType().Name}: {ex.Message}";
            }
        }

        // Splits the input text into chunks of specified size.
        // Avoids Hugging Face Model token limit of 1024.
        private List<string> ChunkText(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, text.Length - i);
                chunks.Add(text.Substring(i, length));
            }
            return chunks;
        }

        // Result class to deserialize HuggingFace response.
        private class SummaryResult
        {
            public string summary_text { get; set; }
        }
    }
}