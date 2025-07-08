# AI News Agent

C# Web API for news aggregation that fetches real-time headline URLs from NewsAPI, extracts full article story content from selected URLs, and summarizes it with AI.

> **Version:** `v1.2-api-release`  
> **Note:** This version introduces a full Web API implementation, building on the CLI version and transitioning to a controller based architecture with Swagger UI, enhanced AI summarization with recursive fallback, structured logging, and centralized Result<T> error handling, now exposed via ProblemDetails.

---

## Features
 
- **`NewsApiService`** - Fetches top headlines from NewsAPI, returning metadata such as title, author, source, published date, and URL.

- **`ArticleBodyService`** – Uses `HttpClient` to fetch article HTML and extract the main text using `HtmlAgilityPack`. 
  - Prioritizes semantic tags like `<article>`
  - Falls back to the `<div>` with the most paragraph content when semantic tags are not present.

- **`PlaywrightRenderService`** – Final fallback when `HttpClient` fails to retrieve HTML.
  - Uses a headless browser to fully render the webpage including JavaScript-heavy pages.
  - Passes rendered HTML back to `ArticleBodyService` for parsing.

- **`AIAnalysisService`** – AI based article summarization using Hugging Face's BART model (`facebook/bart-large-cnn`)
  - Estimates token length of the article and determines if chunking is required to fit within the BART model's token limit.
  - Long articles are split into smaller chunks, each summarized individually, then merged and summarized again for the final summary.
  - Applies dynamic token budgeting to scale each chunk summary, ensuring the merged result stays within the final summary token limit.
  - Short articles skip chunking entirely and use a reduced token budget to prevent AI overgeneration in the final summary.
  - Very large articles enforce a 150 token minimum per chunk summary. If the merged result exceeds the final summary token limit, a recursive fallback system re-chunks and compresses it until it is within budget.
  
- **`HttpHeadersConfig`** – Centralized HTTP headers to ensure consistent requests across services.

- **`Interfaces`** – Improves modularity with dependency injection to support clean architecture and enable easier testing.

- **`Swagger UI`** – Interactive documentation with versioned OpenAPI schema (`v1.2`), XML comment descriptions, and proper `ProblemDetails` for failed requests.

---

## Technologies Used

- **ASP.NET Core Web API** – Backend framework for HTTP routing, DI, and middleware
- **NewsAPI** – Real time news headline data provider
- **Hugging Face Transformers (BART)** – AI summarization model (`facebook/bart-large-cnn`)
- **HtmlAgilityPack** – For parsing and extracting HTML content
- **Microsoft Playwright** – Headless browser fallback for HTML extraction on JavaScript heavy pages
- **Swagger / Swashbuckle** – Interactive API documentation and testing
- **Microsoft.Extensions.Logging** –  Structured logging using dependency injection
- **System.Net.Http + Custom Headers** – Outbound HTTP requests with configurable headers
- **IOptions<T> Configuration Binding** – Strongly typed config settings for API keys and services
- **Result<T> & ProblemDetails** – Centralized success/error result modeling and standardized API error responses

---

## Setup

### 1. API Keys

- **NewsAPI Key** – Required to fetch news headlines. [Get your NewsAPI key here](https://newsapi.org/)
- **Hugging Face API Key** – Required for AI Article summarization. [Get your Hugging Face API key here](https://huggingface.co/)

### 2. Install Playwright

- **Playwright CLI** – Needed to render full web pages when HttpClient extraction fails.

- Install Microsoft Playwright CLI and browsers:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

## Running the Web API

### Launch the API

```bash
dotnet run
```

### Using Swagger to access API endpoints

- Once the API is running, navigate to:

```bash
https://localhost:7044/swagger/index.html
```

- With Swagger you can:
  - Call any endpoint (`/api/Articles/top-headlines`, `/body`, `/summarize`)
  - View detailed endpoint descriptions, parameters, and response schemas
  - Submit a news article URL to extract the full article body or receive an AI-generated summary
  - See example inputs and error responses using `ProblemDetails`
![image](https://github.com/user-attachments/assets/513a7018-af0e-4357-9f8b-bcd34fc81846)

---

## **API Testing**
See [docs/manual-testing.md](docs/manual-testing.md) for more detailed test scenarios, including chunked vs. short article handling, HTML fallbacks, and Playwright usage.

### **Manual Test 1: Top Headlines Retrieval**
1. Once inside Swagger, select the dropdown for `GET /api/Articles/top-headlines`
2. Use the default params or customize `country` and `pageSize`.
3. The API will return a list of the latest top headlines in proper JSON format with a 200 OK status.

### **Manual Test 2: Article Body Fetching** 
1. Inside Swagger enter a headline or news URL under `GET /api/articles/body`
2. The API fetches and parses the raw HTML to extract the article’s main content, using fallback strategies when needed.
3. The extracted body is returned in a JSON response with a 200 OK status. 
4. Invalid URL requests return a 400 Bad Request with a structured ProblemDetails error.

### **Manual Test 3: AI-Powered Article Summarization**
1. Inside Swagger enter a headline or news URL under `GET /api/articles/summarize`
2. The API extracts the article body, splits it into chunks if needed, and summarizes each chunk using AI.
3. All chunk summaries are then merged and re-summarized to generate a final, compressed summary.
4. The API returns a complete AI-generated summary with a 200 OK status and valid JSON format.


> **Note:** Automated unit and integration tests are planned for a future update to complement the current manual testing procedures.

---

## **Screenshots**
**Test 1 Results: Top Headlines Retrieval**
  - Executed `GET /api/Articles/top-headlines` with default params (`country=us`, `pageSize=5`).
  - Received 200 OK with list of top headlines in correct JSON format.
![image](https://github.com/user-attachments/assets/d2dae5f7-d612-4637-a5ad-9ff8513575b0)

---

**Test 2 Results: Article Body Fetching** 
  - Successfully fetched the full article body from a valid URL and returned a 200 OK response.
![image](https://github.com/user-attachments/assets/d994a868-9873-4869-bbbc-0ffe625a914d)

  - Negative test with an invalid URL returned a 400 Bad Request response with a structured ProblemDetails JSON body, confirming proper error handling.
![image](https://github.com/user-attachments/assets/40ffa355-217e-4cc7-9689-fe84f3fd640e)

---

**Test 3 Results: AI-Powered Article Summarization** 
  - The API successfully extracted the article body, split it into multiple chunks, and generated individual AI summaries for each chunk.
![image](https://github.com/user-attachments/assets/8c63f345-54b8-4f73-9dac-0f504b483ab0)

  - The chunk summaries were then merged and re-summarized to produce the final AI-generated summary.
![image](https://github.com/user-attachments/assets/1969f537-523b-4f5b-bfbe-3879f9d91321)

  - The API returned a 200 OK with the final summary in valid JSON format.
![image](https://github.com/user-attachments/assets/18195feb-fcbb-4233-8c48-ccb6f36b1af6)




