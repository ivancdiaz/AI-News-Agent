# AI News Agent

C# console app testing the framework for a news aggregation service using real-time data from the NewsAPI and sample articles.

## Features
- `Article` model to represent news items
- `NewsService` to fetch top news articles through NewsApiService
- `NewsApiService` to fetch real-time top headlines from the NewsAPI
- `OutputService` to print articles to the console

## Setup

### 1. API Key
To fetch live news, you need a **News API key**. You can get one from [NewsAPI](https://newsapi.org/).

### 2. Run the Application
When you run the program, it will prompt you in the terminal to **enter your News API key**. The program uses this key to fetch real-time news.