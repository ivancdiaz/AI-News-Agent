namespace AI.News.Agent.Models
{
    // An article paired with whatever relevance data exists for it. Named for what it is today -
    // a search result - rather than "Ranked", since no ranking/ordering/filtering policy exists yet.
    public class ArticleSearchResult
    {
        public Articles Article { get; set; } = default!;

        // Null when relevance evaluation did not run at all for this request (e.g. Jev/OpenRouter
        // was unavailable). A non-null ArticleRelevance with a null Score means Jev ran but returned
        // no answer for this specific article.
        public ArticleRelevance? Relevance { get; set; }
    }
}
