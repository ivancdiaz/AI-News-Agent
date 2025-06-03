# AI News Agent

C# console app prototype for news aggregation that fetches real-time headline URLs from NewsAPI, extracts full article story content from selected URLs, and summarizes it with AI.

> **Version:** `v1.0-preapi`  
> **Note:** This is the CLI-based version. A full web API version is planned as the next major update.                   

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
  - Dynamically estimates token length of the text and splits long articles into chunks to fit within BART's token limit.
  - Summarizes each chunk individually, then merges them all into a final summary.
  - Skips chunking for short articles and computes a custom summarization token budget based on article length.
  - Uses token budgeting to prevent overgeneration: short articles get scaled-down budgets, while chunked articles stay within a fixed final summary budget across all stages.

- **`HttpHeadersConfig`** – Centralized HTTP headers to ensure consistent requests across services.

- **`Interfaces`** – Improves modularity with dependency injection to support clean architecture and enable easier testing.

---

## Setup

### 1. API Keys

- **NewsAPI Key** – Used to fetch news headlines. [Get your NewsAPI key here](https://newsapi.org/)
- **Hugging Face API Key** – Required for AI Article summarization. [Get your Hugging Face API key here](https://huggingface.co/)

### 2. Install Playwright

- **Playwright CLI** – Needed to render full web pages when HttpClient extraction fails.

- Install Microsoft Playwright CLI and browsers:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

### 3. Run the App

```bash
dotnet run
```

- You’ll be prompted to enter a NewsAPI key or bypass by entering `skip`. This key is required to see top headlines.
- Next you'll be asked to enter a Hugging Face API key, which is required for article summarization.
- If using NewsAPI, the app will then list 5 current top news stories.
- Finally you'll be asked to enter a URL, you can enter your own or copy and paste one from the list of headlines.
- The application will fetch, extract, and summarize the article content.
- The terminal will display:
  - The full article text
  - Each chunk summary used (for articles longer than 1024 tokens)
  - The final AI-generated summary

---

## **Testing**

### **Manual Test 1: End-to-End Run and Short Article Summarization**
1. Enter valid NewsAPI and Hugging Face API keys.  
2. Select an article from the top headlines and confirm the full flow: display ➝ extract ➝ summarize.  
3. Test with a short article that fits under the max token chunk size to ensure chunking is skipped.  
4. Validate the summary output, including logs showing token counts and chunking behavior.

### **Manual Test 2: Summarize Long Article (Chunked Summarization)**  
1. Provide a longer article that requires chunking.  
2. Confirm that the article is split into chunks.  
3. Each chunk is summarized with the assigned token budget.  
4. Final summary combines chunk summaries respecting the token budget.

### Manual Test 3: Article Extraction Fallbacks
1. Run the app with a URL where `<article>` and common semantic tags are missing.
2. Verify it falls back to selecting the `<div>` with the most paragraph text.
3. Look for this console message when fallback is used:  
   `"[INFO] Fallback: Found div with most paragraph content."`

### **Manual Test 4: Article Extraction with Playwright Fallback**
1. Run the application and provide a URL where `HttpClient` fails or returns minimal HTML.
2. Verify that Playwright is used to render and extract the full article body.
3. Confirm that a summary is generated from the extracted content.

---

## **Screenshot**
**Test 1 Results:** End-to-end article fetch, extraction, and summary with chunking for short articles
![image](https://github.com/user-attachments/assets/c1b79774-8dea-4c35-a893-f1a99303acd3)

**Test 2 Results:** Output showing chunk summary lengths and final summary token counts all within the set budgets
![image](https://github.com/user-attachments/assets/18c1ed6b-5930-4346-b77c-561d2138e0dc)

**Test 3 Results:** HTML was not found prior to adding the new final fallback for HttpClient
![image](https://github.com/user-attachments/assets/9732163e-75d9-4866-a090-fe3c4eec208a)

**Test 3 Results:** Using the final fallback for HttpClient that selects the div with the most paragraph text
![image](https://github.com/user-attachments/assets/c7516593-e1fb-4fd6-aabe-27324b09db6d)

**Test 4 Results:** After forcing HttpClient to fail and falling back to Playwright to render and fetch HTML
![image](https://github.com/user-attachments/assets/6779ff22-2e4c-44bc-9a9a-ec6d13ed9888)
![image](https://github.com/user-attachments/assets/38fe4f92-aac0-4c07-9be2-3952366f6a8f)

