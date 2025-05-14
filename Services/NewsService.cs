using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.News.Agent.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AI.News.Agent.Services
{
public class NewsService
{
    private readonly NewsApiService _apiService;

    // Inject a news provider (NewsApiService)
    public NewsService(NewsApiService apiService)
    {
        _apiService = apiService;
    }

    // Delegate the call to the provider service
    public async Task<List<Articles>> GetNewsAsync()
    {
        return await _apiService.FetchTopHeadlinesAsync();
    }
}
}