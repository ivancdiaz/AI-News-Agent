namespace AI.News.Agent.Models
{
    // Structured request shape produced by IQueryParserService, consumed by NewsApiService.SearchArticlesAsync
    public class ArticleSearchQuery
    {
        public string Query { get; set; } = default!;
        public DateTime From { get; set; }
        public string Language { get; set; } = "en";
        public string SortBy { get; set; } = "relevancy";
        public int PageSize { get; set; } = 5;
    }
}
