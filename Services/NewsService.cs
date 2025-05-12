using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.News.Agent.Models;

namespace AI.News.Agent.Services
{
    public class NewsService
    {
        private readonly NewsApiService _apiService;

        public NewsService(string apiKey)
        {
            _apiService = new NewsApiService(apiKey); // Pass the API key to NewsApiService
        }

        public async Task<List<Articles>> GetNewsAsync()
        {
            return await _apiService.FetchTopHeadlinesAsync();
        }
    }
}