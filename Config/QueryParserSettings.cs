namespace AI.News.Agent.Config
{
    // Query parser endpoint, model id, and the defaults/limits applied to model output.
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
