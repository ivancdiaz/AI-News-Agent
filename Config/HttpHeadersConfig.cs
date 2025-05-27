namespace AI.News.Agent.Config
{
    public static class HttpHeadersConfig
    {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                                        "Chrome/125.0.0.0 Safari/537.36";

        public static readonly Dictionary<string, string> DefaultHeaders = new()
        {
            { "User-Agent", UserAgent },
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Referer", "https://www.google.com/" },
            { "Upgrade-Insecure-Requests", "1" },
            { "DNT", "1" }
        };
    }
}