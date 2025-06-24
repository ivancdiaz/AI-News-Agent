using Newtonsoft.Json;

namespace AI.News.Agent.Models
{
    public class Summary
    {
        // Map the Hugging Face "summary_text" JSON field to Text property
        [JsonProperty("summary_text")]
        public string Text { get; set; } = string.Empty;
    }
}