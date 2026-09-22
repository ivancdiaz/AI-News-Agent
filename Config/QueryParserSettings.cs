namespace AI.News.Agent.Config
{
    // Settings for the query parser: endpoint, model id, and the defaults/limits it applies
    // when validating model output. Kept separate from ArticleSummarizationSettings, which only configures BART.
    // Defaults below apply unless overridden under "AI:QueryParser" in configuration.
    public class QueryParserSettings
    {
        public string Url { get; set; } = default!;
        public string Model { get; set; } = default!;

        // Used when the model omits an optional field
        public string DefaultLanguage { get; set; } = "en";
        public string DefaultSortBy { get; set; } = "relevancy";
        public int DefaultPageSize { get; set; } = 5;
        public int DefaultLookbackDays { get; set; } = 7;

        // Limits applied to model output
        public int MaxPageSize { get; set; } = 10;
        public int MaxLookbackDays { get; set; } = 29; // NewsAPI developer plan only serves about one month of history
        public int MaxQueryLength { get; set; } = 500; // NewsAPI 'q' limit
    }
}
