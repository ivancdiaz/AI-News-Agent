namespace AI.News.Agent.Models
{
    public class Article
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Source { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Content { get; set; }
    }
}