namespace AI.News.Agent.Config
{
    // Application-controlled search policy: how many candidates to fetch for Jev to evaluate,
    // and the minimum relevance score required to keep a result. Independent of Qwen and Jev.
    public class ArticleSearchSettings
    {
        public int CandidatePoolSize { get; set; } = 20;
        public double MinRelevanceScore { get; set; } = 0.50;
    }
}
