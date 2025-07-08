# Manual Testing Guide

_See `README.md` for setup instructions, including API key configuration and how to launch the application._

This document outlines detailed manual testing steps for the AI.News.Agent API.

### **Manual Test 1: Article Extraction with Div Based Fallback**
1. Inside Swagger enter a headline or news URL under `GET /api/articles/body`
2. Test with a URL where `<article>` and common semantic tags are missing.
3. Verify it falls back to selecting the `<div>` with the most paragraph text.
4. Confirm full body extraction is returned in a JSON response with a 200 OK status.
5. Look for this console message showing when fallback was used: 
        `"Fallback: Selected <div> with most paragraph content."`


### **Manual Test 2: Article Extraction with Playwright Fallback**
1. Inside Swagger enter a headline or news URL under `GET /api/articles/body`
2. Provide a URL where `HttpClient` fails or returns minimal HTML.
3. Verify that Playwright is used to render and extract the full article body.
4. Confirm full body extraction is returned in a JSON response with a 200 OK status.
5. Look for these console messages showing when fallback was used:
        `"HttpClient fetch succeeded, but parsing returned no usable content."`
        `"Falling back to Playwright to render the page."`


### **Manual Test 3: Short Article End-to-End Summarization without Chunking**
1. Inside Swagger enter a headline or news URL under `GET /api/articles/summarize`
2. Test with a short article that fits under the maximum tokens per chunk size to ensure chunking is skipped. 
3. Short article body is extracted, if token count is within the limit, chunking is skipped and the full body is summarized using AI.
4. The API returns a complete AI-generated summary with a 200 OK status and valid JSON format.
5. Validate logs show correct token counts and the chunking behavior was skipped.
        `"Article fits within a single chunk, skipping chunking."`


### **Manual Test 4: Article End-to-End Summarization with Chunking** 
1. Inside Swagger enter a headline or news URL under `GET /api/articles/summarize`
2. The API extracts the article body, splits it into chunks and summarizes each chunk using AI.
3. All chunk summaries are then merged and re-summarized to generate a final, compressed summary.
4. The API returns a complete AI-generated summary with a 200 OK status and valid JSON format.
5. Validate logs show correct token counts and the chunking behavior was not skipped.
        `"Summarizing chunk #1 (Length: 2731 chars, ~682 tokens)"`
        `"Summarizing chunk #2 (Length: 2731 chars, ~682 tokens)"`
        `"Summarizing chunk #3 (Length: 2729 chars, ~682 tokens)"`


### **Manual Test 5: Large Article End-to-End Summarization with Recursive Re-chunking Fallback**
1. Inside Swagger use `GET /api/articles/summarize` and enter a news URL for summarizing. 
2. Test with a large article, the initial chunk summary attempt triggers a minimum token budget of 150.
3. Result shows successful summarization of the large article using multi pass summarization logic, dynamic re-chunking and compression.
4. The API returns a complete AI-generated summary with a 200 OK status and valid JSON format.
5. Observe logs, min token budget of 150 is set, confirm the re-chunking fallback activates when the max token limit of 900 is exceeded.
        `"Chunk summary token budget too small (75); using minimum of 150."`
        `"Combined chunk summaries exceed final summarization token limit (~1733 > 900). Re-chunking..."`

---

## **Screenshots**
**Test 1 Results: Article Extraction using Div Based Fallback** 
  - When semantic tags or common article body containers aren’t found, it falls back to selecting the `<div>` with the most paragraph text.
![image](https://github.com/user-attachments/assets/d766e08c-cb92-422b-8f34-977538064d85)

  - The extracted article body is displayed in Swagger with a 200 OK response.
![image](https://github.com/user-attachments/assets/bb89aadd-5804-4333-803c-a3b20f08972c)


**Test 2 Results: Article Extraction using Playwright Fallback** 
  - After HttpClient fails to fetch usable HTML, it falls back to Playwright to render the web page and fetch HTML.
  - The raw HTML is then parsed to extract the article’s main content.
![image](https://github.com/user-attachments/assets/6732cdbe-36cc-4ab4-8541-510678cab1de)

  - The extracted article body is displayed in Swagger with a 200 OK response.
![image](https://github.com/user-attachments/assets/d593a15e-a5c7-422e-b8c8-a2df9dd4e600)


**Test 3 Results: Short Article End-to-End Summarization without Chunking** 
  - End-to-end article fetch, extraction, and summary without chunking for short articles.
![image](https://github.com/user-attachments/assets/06de45fd-8f52-478e-b350-6431cc8bc1d2)

  - Swagger displays the final summary with a 200 OK response.
![image](https://github.com/user-attachments/assets/010e1e50-35da-4b81-8be8-87d000c83ea8)


**Test 4 Results: Article End-to-End Summarization with Chunking** 
  - The API successfully extracted the article body, split it into multiple chunks, and generated individual AI summaries for each chunk.
![image](https://github.com/user-attachments/assets/adb94013-8234-4b08-840c-d41bfc7b3958)

  - The chunk summaries were then merged and re-summarized to produce the final AI-generated summary.
  - Logs show chunk summary lengths and final summary token counts all within the set budgets.
![image](https://github.com/user-attachments/assets/1969f537-523b-4f5b-bfbe-3879f9d91321)

  - Swagger displays the final summary with a 200 OK response.
![image](https://github.com/user-attachments/assets/18195feb-fcbb-4233-8c48-ccb6f36b1af6)


**Test 5 Results: Large Article End-to-End Summarization with Recursive Re-chunking Fallback** 
  - Using a large article with 43,101 chars, enforces a minimum token budget.
  - This sets the minimum token budget per chunk summary to be set at 150 tokens.
![image](https://github.com/user-attachments/assets/0aee0aec-452e-40b1-8e84-eb6c31d2f25a)

  - After combining the chunk summaries it will now be over budget for final summary (~1733 > 900).
  - This triggers the re-chunking fallback, which reduces final summary size to fit within the max tokens per chunk budget of 900.
![image](https://github.com/user-attachments/assets/87e15a59-9e69-4f20-9194-6ba118f4f04d)

  - This all results in a successful summarization of the large article using 12 chunk summaries.
  - The final summary is created with 284 tokens.
  - Swagger displays the final summary with a 200 OK response.
![image](https://github.com/user-attachments/assets/3d41bcc2-d60d-4a3f-a091-21e4b88e5f3a)

---

**Reference: Quick Explanation of Chunking & Summary Budget Logic** 
1. Estimate total token count of the article body.

2. Calculate the number of chunks:
   - If total article tokens > max tokens per chunk budget:
   - (Number of chunks = Article tokens / Max tokens per chunk)

3. Chunking Strategy:
   - The article is then split into chunks of roughly equal size.
   - (Size of each chunk = Article tokens / Number of chunks)

4. Summary token budgeting:
   - Each chunk is summarized with a dynamic token budget.
   - Ensuring the combined chunks summaries stay under the max tokens per chunk limit for the final summary.
   - (Dynamic token budget = Max tokens per chunk / Number of chunks)

5. Fallback to preserve summary quality when dynamic token budget is set too low:
   - If the dynamic token budget result for each chunk is less than (75 tokens), enforce a minimum token budget of (150 tokens).       
   - The enforced minimum causes the combined chunk summaries to exceed the max tokens per chunk budget. 
   - The system will then recursively re-chunk and compress the merged summary until it fits within the max token budget.


