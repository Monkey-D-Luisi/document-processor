# GPT-5.6 Luna and GPT-6 Luna Evaluation

Date: October 21, 2026. Environment: local Release build, PostgreSQL in Docker, live Responses API and five synthetic Spanish-language documents. The same five files were sent to each model in alternating order: a PDF invoice, a scanned invoice as PNG, a multiline invoice, an inconsistent-total invoice as JPEG, and a purchase order that is not an invoice.

## Results

| Model | Completed responses | Exact ground truth | Median latency | Input tokens | Output tokens | Estimated cost |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| gpt-5.6-luna | 5 | 108/108 | 3,095 ms | 10,367 | 959 | $0.003601 |
| gpt-6-luna | 5 | 108/108 | 3,602 ms | 10,367 | 1,089 | $0.001769 |

GPT-6 Luna's median latency was 16.4% higher in this run. Its estimated cost for the five documents was 50.9% lower. Both models returned the configured model ID in all five responses.

Each model's set had 108 exact checks: 13 header and review fields per document, line count, six fields per line, and required issue codes. Both models extracted the checked values correctly, marked the inconsistent-total invoice for review and classified the purchase order as Unknown. GPT-6 Luna added a descriptive warning for one image; the required total_mismatch issue code was also present.

## Issue during testing

The first image upload failed in the local process with ArgumentNullException (MediaType): the adapter created BinaryData without a MIME type, which the .NET SDK requires when constructing an image input. The exception occurred before calling the OpenAI client, with 0 ms provider latency and no tokens. The validated MIME type was passed to BinaryData and three regression tests were added for PNG, JPEG and WebP. The ten authorized live responses were then completed. The local API received eleven submissions, but only ten provider requests were made.

## Method and limitations

The ground truth was reviewed against the five synthetic originals. Both models used the same build, prompt, JSON schema, validation policies and document data. Latency is measured by the adapter from request construction until the response arrives; it excludes PostgreSQL persistence and UI rendering. Cost combines actual token usage returned by each response with the configured rates, including cache read and write tokens. It is an estimate, not the amount billed by OpenAI. Configured rates were checked against the official [GPT-5.6 Luna documentation](https://developers.openai.com/api/docs/models/gpt-5.6-luna), [GPT-6 Luna documentation](https://developers.openai.com/api/docs/models/gpt-6-luna) and [API pricing page](https://developers.openai.com/api/docs/pricing).

Five documents do not represent the real variation in providers, scans, languages, formats or accounting cases. These results do not prove that GPT-6 Luna is generally more intelligent or accurate; they show equal exact-match results on this sample, lower estimated cost and higher observed latency.

## Decision

Set gpt-6-luna as the default and keep both models configurable. This reflects the project owner's preference and the observed results: identical accuracy on this sample at lower cost, with a slower median. To switch to GPT-5.6 Luna for a session, set OpenAI__Model=gpt-5.6-luna.

The synthetic test documents, corrected ground truth and responses with telemetry are in [the evaluation artifacts](../artifacts/model-evaluation-2026-10-21/).
