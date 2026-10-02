# Manual Testing Guide

_See `README.md` for setup instructions, including API key configuration and how to launch the application._

This document provides detailed manual validation for the primary AI.News.Agent workflows, including natural-language news search, semantic relevance evaluation, article extraction, fallback behavior, and token-aware article summarization.

### Manual Test 1: Article Extraction with Div-Based Fallback

1. In Swagger, use `GET /api/Articles/body` with a news article URL.
2. Test with a URL where `<article>` and common semantic article containers are missing.
3. Verify the extraction process falls back to selecting the `<div>` with the most paragraph content.
4. Confirm the extracted article body is returned with HTTP 200.
5. Verify the application logs show that the fallback was used:

   `"Fallback: Selected <div> with most paragraph content."`

**Expected result:** The article body is successfully extracted using the div-based fallback and returned in the API response.

[View Test 1 screenshots](#test-1-results-article-extraction-with-div-based-fallback)

### Manual Test 2: Article Extraction with Playwright Fallback

1. In Swagger, use `GET /api/Articles/body` with a news article URL.
2. Test with a URL where `HttpClient` fails to retrieve usable article content or returns minimal HTML.
3. Verify Playwright renders the page and the rendered HTML is passed back through the extraction process.
4. Confirm the extracted article body is returned with HTTP 200.
5. Verify the application logs show the Playwright fallback:

   `"HttpClient fetch succeeded, but parsing returned no usable content."`

   `"Falling back to Playwright to render the page."`

**Expected result:** When normal HTTP retrieval does not provide usable content, Playwright renders the page and the article body is successfully extracted.

[View Test 2 screenshots](#test-2-results-article-extraction-with-playwright-fallback)

### Manual Test 3: Short Article End-to-End Summarization without Chunking

1. In Swagger, use `GET /api/Articles/summarize` with a short news article URL.
2. Confirm the article body is extracted successfully.
3. Verify the estimated token count fits within the maximum tokens-per-chunk limit.
4. Confirm chunking is skipped and the complete article body is sent directly for BART summarization.
5. Confirm the API returns the generated summary with HTTP 200.
6. Verify the application logs show that chunking was skipped:

   `"Article fits within a single chunk, skipping chunking."`

**Expected result:** A short article is extracted and summarized directly without entering the chunking workflow.

[View Test 3 screenshots](#test-3-results-short-article-summarization-without-chunking)

### Manual Test 4: Article End-to-End Summarization with Chunking

1. In Swagger, use `GET /api/Articles/summarize` with an article large enough to exceed the single-chunk token limit.
2. Confirm the article body is extracted and divided into multiple chunks.
3. Verify each chunk is summarized individually using BART.
4. Confirm the chunk summaries are merged and summarized again to generate the final compressed summary.
5. Confirm the API returns the final summary with HTTP 200.
6. Verify the application logs show the individual chunk operations, for example:

   `"Summarizing chunk #1 (Length: 2731 chars, ~682 tokens)"`

   `"Summarizing chunk #2 (Length: 2731 chars, ~682 tokens)"`

   `"Summarizing chunk #3 (Length: 2729 chars, ~682 tokens)"`

**Expected result:** The article is successfully processed through the multi-chunk BART workflow and returned as a final combined summary.

[View Test 4 screenshots](#test-4-results-article-summarization-with-chunking)

### Manual Test 5: Large Article End-to-End Summarization with Recursive Re-chunking

1. In Swagger, use `GET /api/Articles/summarize` with a large news article URL.
2. Confirm the article requires multiple chunks and that the calculated per-chunk summary budget falls below the configured minimum.
3. Verify the minimum summary budget of 150 tokens is applied.
4. Confirm the combined chunk summaries exceed the final summarization token limit.
5. Verify recursive re-chunking and compression are triggered until the combined summary fits within the allowed input budget.
6. Confirm the API returns the final summary with HTTP 200.
7. Verify the application logs show the minimum budget and recursive fallback:

   `"Chunk summary token budget too small (75); using minimum of 150."`

   `"Combined chunk summaries exceed final summarization token limit (~1733 > 900). Re-chunking..."`

**Expected result:** A large article is successfully summarized even when the initial combined chunk summaries exceed BART's final input limit. The recursive compression workflow reduces the intermediate content until final summarization can complete.

[View Test 5 screenshots](#test-5-results-large-article-summarization-with-recursive-re-chunking)

### Manual Test 6: Natural-Language News Search with Qwen

1. In Swagger, use `GET /api/Articles/search` with the query:

   `Whats happening in AI currently?`

2. Confirm Qwen interprets the natural-language request and generates structured NewsAPI search parameters.
3. Verify the generated `ArticleSearchQuery` contains:

   - `Query: AI`
   - `From: 09/15/2026 00:00:00`
   - `Language: en`
   - `SortBy: publishedAt`

4. Confirm NewsAPI successfully executes the generated search parameters.
5. Verify the application logs show the Qwen request, successful HTTP response, and generated `ArticleSearchQuery`.
6. Confirm the search endpoint returns HTTP 200 with AI-related articles.

**Expected result:** Qwen interprets the request as a current AI news search and converts the user's natural-language intent into a structured `ArticleSearchQuery` that can be executed by NewsAPI.

[View Test 6 screenshot](#test-6-results-natural-language-news-search-with-qwen)

### Manual Test 7: Jev Relevance Evaluation, Filtering, and Ranking

1. In Swagger, use `GET /api/Articles/search` with the query:

   `Find recent news articles about artificial intelligence products, not AI company funding or stock prices`

2. Confirm Qwen converts the natural-language request into structured NewsAPI search parameters without controlling the candidate result count.
3. Verify the application requests up to 20 candidate articles from NewsAPI.
4. Confirm the retrieved candidates are evaluated against the original natural-language request in a single batched Jev relevance request.
5. Verify articles with relevance scores below the configured `0.50` threshold are removed.
6. Confirm all qualifying articles are returned in descending relevance-score order.

**Expected result:** NewsAPI returns 20 candidate articles and Jev evaluates all 20 in one batched request. After the application applies the `0.50` minimum relevance threshold, four articles remain with relevance scores of `0.95`, `0.93`, `0.87`, and `0.66`, returned in descending order.

[View Test 7 screenshot](#test-7-results-jev-relevance-evaluation-filtering-and-ranking)

### Manual Test 8: Jev Failure and Graceful Degradation

1. Temporarily change the configured OpenRouter Decisions API URL to an invalid endpoint.
2. In Swagger, use `GET /api/Articles/search` with:

   `Show me recent news about Xbox games, not Xbox hardware or accessories`

3. Confirm Qwen successfully interprets the request and NewsAPI returns candidate articles.
4. Verify the Jev/OpenRouter request fails with HTTP 404 and the relevance evaluation failure is logged.
5. Confirm the overall search endpoint still returns HTTP 200.
6. Verify the original NewsAPI articles are returned without relevance scores and contain `"relevance": null`.
7. Restore the valid OpenRouter Decisions API URL after completing the test.

**Expected result:** Jev relevance evaluation fails as expected, but the search workflow continues successfully. The API returns the NewsAPI articles with `"relevance": null`, confirming that Jev evaluation gracefully degrades rather than causing the entire search request to fail.

---

## Screenshots

### Test 1 Results: Article Extraction with Div-Based Fallback

When semantic tags or common article-body containers are not found, extraction falls back to selecting the `<div>` containing the most paragraph content.

![Div-based article extraction fallback](https://github.com/user-attachments/assets/d766e08c-cb92-422b-8f34-977538064d85)

The extracted article body is displayed in Swagger with an HTTP 200 response.

![Div-based article extraction response](https://github.com/user-attachments/assets/bb89aadd-5804-4333-803c-a3b20f08972c)

### Test 2 Results: Article Extraction with Playwright Fallback

When `HttpClient` does not provide usable article content, the application falls back to Playwright to render the page before extracting the article body.

![Playwright article extraction fallback](https://github.com/user-attachments/assets/6732cdbe-36cc-4ab4-8541-510678cab1de)

The extracted article body is displayed in Swagger with an HTTP 200 response.

![Playwright article extraction response](https://github.com/user-attachments/assets/d593a15e-a5c7-422e-b8c8-a2df9dd4e600)

### Test 3 Results: Short Article Summarization without Chunking

The article is fetched, extracted, and summarized directly without chunking because its estimated token count fits within the single-chunk limit.

![Short article summarization logs](https://github.com/user-attachments/assets/06de45fd-8f52-478e-b350-6431cc8bc1d2)

Swagger displays the final summary with an HTTP 200 response.

![Short article summarization response](https://github.com/user-attachments/assets/010e1e50-35da-4b81-8be8-87d000c83ea8)

### Test 4 Results: Article Summarization with Chunking

The API extracts the article body, divides it into multiple chunks, and generates an individual BART summary for each chunk.

![Article chunking and summarization](https://github.com/user-attachments/assets/adb94013-8234-4b08-840c-d41bfc7b3958)

The chunk summaries are merged and summarized again. Application logs show the chunk summary lengths and final summary token counts remaining within the configured budgets.

![Chunk summary and token logs](https://github.com/user-attachments/assets/1969f537-523b-4f5b-bfbe-3879f9d91321)

Swagger displays the final summary with an HTTP 200 response.

![Chunked article summarization response](https://github.com/user-attachments/assets/18195feb-fcbb-4233-8c48-ccb6f36b1af6)

### Test 5 Results: Large Article Summarization with Recursive Re-chunking

A large article containing 43,101 characters causes the calculated per-chunk summary budget to fall below the minimum, so the application enforces the 150-token minimum.

![Minimum chunk summary token budget](https://github.com/user-attachments/assets/0aee0aec-452e-40b1-8e84-eb6c31d2f25a)

After the chunk summaries are combined, the intermediate content exceeds the final summarization input budget (`~1733 > 900`). This triggers recursive re-chunking and compression.

![Recursive re-chunking fallback](https://github.com/user-attachments/assets/87e15a59-9e69-4f20-9194-6ba118f4f04d)

The large article is successfully processed using 12 chunk summaries, producing a final 284-token summary.

Swagger displays the final summary with an HTTP 200 response.

![Large article final summary](https://github.com/user-attachments/assets/3d41bcc2-d60d-4a3f-a091-21e4b88e5f3a)

### Test 6 Results: Natural-Language News Search with Qwen

The natural-language request is sent to Qwen and mapped to a structured `ArticleSearchQuery` containing the generated search terms, date range, language, and sort order.

For the request:

`Whats happening in AI currently?`

Qwen generated:

- `Query: AI`
- `From: 09/15/2026 00:00:00`
- `Language: en`
- `SortBy: publishedAt`

<img width="889" height="169" alt="Qwen natural-language query mapped to structured ArticleSearchQuery" src="https://github.com/user-attachments/assets/f73b5981-137c-449e-9141-c5d080c19290" />

### Test 7 Results: Jev Relevance Evaluation, Filtering, and Ranking

The natural-language AI-products request produces 20 NewsAPI candidates that are evaluated in a single batched Jev request. After applying the `0.50` threshold, four qualifying articles remain in descending relevance order.

The highest-ranked results have relevance scores of `0.95` and `0.93`.

<img width="1431" height="1192" alt="Qwen and Jev natural-language news search with relevance-ranked results" src="https://github.com/user-attachments/assets/0349d371-fd59-4885-8345-b312f637a6d4" />

> Tests 6 and 7 include visual evidence of the natural-language search workflow. Test 8 is documented as a reproducible failure-path scenario demonstrating graceful degradation when Jev relevance evaluation is unavailable.

---

## Reference: Quick Explanation of Chunking & Summary Budget Logic

1. Estimate the total token count of the extracted article body.

2. Determine whether the article exceeds the maximum tokens-per-chunk budget.

3. If chunking is required, divide the article into chunks of roughly equal size:

   `Chunk size ≈ Article tokens / Number of chunks`

4. Calculate a dynamic summary budget for each chunk so the combined summaries can remain within the input budget for final summarization:

   `Dynamic token budget = Max tokens per chunk / Number of chunks`

5. If the calculated per-chunk summary budget is too small, enforce the configured minimum summary budget of 150 tokens.

6. If enforcing the minimum causes the combined chunk summaries to exceed the 900-token final input budget, recursively re-chunk and compress the merged summaries.

7. Continue compression until the combined summary fits within the allowed input budget, then generate the final BART summary.
