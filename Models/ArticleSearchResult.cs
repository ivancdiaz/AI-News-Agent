namespace AI.News.Agent.Models
{
    // Article with optional relevance data from semantic evaluation.
    public class ArticleSearchResult
    {
        public Articles Article { get; set; } = default!;

        // Null = relevance evaluation didn't run at all (e.g. Jev/OpenRouter unavailable).
        // Non-null with a null Score = Jev ran but had no answer for this article.
        public ArticleRelevance? Relevance { get; set; }
    }
}
