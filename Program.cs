using AI.News.Agent.Services;
using AI.News.Agent.Output;


namespace AI.News.Agent
{
    class Program
    {
        static void Main(string[] args)
        {
            var newsService = new NewsService();
            var outputService = new OutputService();

            var articles = newsService.GetSampleArticles();
            outputService.DisplayArticles(articles);
        }
    }
}