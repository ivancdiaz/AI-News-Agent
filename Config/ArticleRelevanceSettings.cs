namespace AI.News.Agent.Config
{
    // External configuration for the Jev relevance evaluator (OpenRouter Decisions API endpoint + model id).
    // The Noul question text and criteria are owned by ArticleRelevanceService itself, not config,
    // matching how QueryParserService owns its own prompt.
    public class ArticleRelevanceSettings
    {
        public string Url { get; set; } = default!;
        public string Model { get; set; } = default!;
    }
}
