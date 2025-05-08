using System;
using System.Collections.Generic;
using AI.News.Agent.Models;
using static System.Console;


namespace AI.News.Agent.Output
{
    public class OutputService
    {
        public void DisplayArticles(List<Article> articles)
        {
            foreach (var article in articles)
            {
                WriteLine("📰 " + article.Title);
                WriteLine("   By: " + article.Author);
                WriteLine("   Source: " + article.Source);
                WriteLine("   Published: " + article.PublishedAt);
                WriteLine("   " + article.Content);
                WriteLine(new string('-', 50));
            }
        }
    }
}