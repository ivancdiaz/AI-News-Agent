namespace AI.News.Agent.Models
{
    public class Articles
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Source { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Description { get; set; }
        public string? SourceUrl { get; set; }
    }
}