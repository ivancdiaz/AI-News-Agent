namespace AI.News.Agent.Config
{
    // Endpoint + model id for the Jev relevance evaluator. Question text and criteria live in
    // ArticleRelevanceService itself, not here - same split QueryParserService uses for its prompt.
    public class ArticleRelevanceSettings
    {
        public string Url { get; set; } = default!;
        public string Model { get; set; } = default!;
    }
}
