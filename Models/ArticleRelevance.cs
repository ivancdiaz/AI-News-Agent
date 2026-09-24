namespace AI.News.Agent.Models
{
    // Score is Jev's Noul probability (0.0-1.0). null means no result for this article
    // (call failed, or this article's answer was missing from the batch) - not a score of 0.
    public class ArticleRelevance
    {
        public double? Score { get; set; }
    }
}
