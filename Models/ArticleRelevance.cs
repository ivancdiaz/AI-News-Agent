namespace AI.News.Agent.Models
{
    // Jev's relevance opinion for one article. Score is a Noul probability (0.0-1.0).
    // null means no result exists for this article (evaluation call failed, or this
    // article's answer was missing from an otherwise successful batch) - not a score.
    public class ArticleRelevance
    {
        public double? Score { get; set; }
    }
}
