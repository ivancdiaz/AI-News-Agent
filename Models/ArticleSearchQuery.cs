namespace AI.News.Agent.Models
{
    // Structured request shape produced by IQueryParserService, consumed by NewsApiService.SearchArticlesAsync
    public class ArticleSearchQuery
    {
        public string Query { get; set; } = default!;
        public DateTime From { get; set; }
        public string Language { get; set; } = default!;
        public string SortBy { get; set; } = default!;
        public int PageSize { get; set; }
    }
}
