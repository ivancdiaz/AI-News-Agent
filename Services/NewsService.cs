using System;
using System.Collections.Generic;
using AI.News.Agent.Models;


namespace AI.News.Agent.Services
{
    public class NewsService
    {
        // Test method to get sample articles
        public List<Article> GetSampleArticles()
        {
            return new List<Article>
            {
                new Article
                {
                    Title = "AI Breakthrough Revolutionizes News Aggregation",
                    Author = "Nancy Doe",
                    Source = "TechDaily",
                    PublishedAt = DateTime.UtcNow.AddHours(-2),
                    Content = "A new AI-powered system is changing the way we consume news..."
                },
                new Article
                {
                    Title = "OpenAI Releases New Model Capable of Real-Time Fact-Checking",
                    Author = "John Smith",
                    Source = "AI Weekly",
                    PublishedAt = DateTime.UtcNow.AddHours(-5),
                    Content = "The latest GPT update introduces a feature that enables real-time verification..."
                }
            };
        }
    }
}