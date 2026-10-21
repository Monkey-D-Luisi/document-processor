# Document Processor — Case Study 02

A local .NET demo that extracts business data from PDFs and images, validates
the resulting candidate deterministically, persists the result, and marks
uncertain or inconsistent data for human review.

## Scope

The demo recognizes invoices, delivery notes, quotes and unknown documents. It
accepts PDF, JPEG, PNG and WebP uploads up to 20 MiB.

The flow is:

```text
browser upload -> API validation -> application use case -> OpenAI Responses
-> strict JSON candidate -> deterministic business checks -> PostgreSQL -> UI
```

The OpenAI key stays in the backend. The model response is never treated as a
trusted domain object. The application checks required fields, currency/date
format, line extensions, discounts, tax bases and amounts, surcharges,
withholdings and payable totals. Missing or ambiguous values, model warnings,
unknown document types and arithmetic differences require human review. A
review-free result means the checks passed; it does not prove the source values
are correct.

## Architecture

```text
DocumentProcessor.API -> Application -> Domain
          |                  ^
          +-> Infrastructure-+
                 |       |
     OpenAI provider  PostgreSQL
```

- `Domain` owns the document lifecycle and has no EF Core, OpenAI or HTTP
  dependency.
- `Application` owns CQRS use cases, the `IDocumentAnalyzer` port, provider
  contracts and factory, validation, and extraction policy.
- `Infrastructure` implements PostgreSQL persistence and the OpenAI provider.
- `API` owns upload boundaries, antiforgery, HTTP error handling and the demo UI.

The provider ID is configured separately from the OpenAI model with
`DocumentAnalysis:Provider`. The currently implemented and supported ID is
`openai`; unknown IDs fail startup validation. `OpenAI:Model` independently
selects `gpt-6-luna` (default) or `gpt-5.6-luna`.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Windows local setup

Requirements: .NET 10 SDK, Docker Desktop and an OpenAI API key with access to
the selected API model.

From PowerShell in the repository root:

```powershell
docker compose up -d
dotnet restore .\DocumentProcessor.sln
dotnet build .\DocumentProcessor.sln --configuration Release
dotnet test .\DocumentProcessor.sln --configuration Release
```

The PostgreSQL container is bound to `127.0.0.1:5438`. The Development startup
applies pending EF Core migrations; start the database before starting the API.

Set the key for this PowerShell session without printing or committing it, then
run the API:

```powershell
$keyFile = 'C:\path\to\openai_api_key.txt'
$env:OpenAI__ApiKey = (Get-Content -Raw $keyFile).Trim()
try {
    dotnet run --project .\src\DocumentProcessor.API\DocumentProcessor.API.csproj
}
finally {
    Remove-Item Env:OpenAI__ApiKey -ErrorAction SilentlyContinue
}
```

Use the actual path to your key file. For example, keep it outside the
repository at `$env:USERPROFILE\secrets\openai_api_key.txt`.
Open <http://localhost:5178> and upload a synthetic or approved test document.
The browser obtains an antiforgery token before sending the upload.

The API endpoint reference is:

- `GET /api/v1/documents/antiforgery` — obtains a request token and cookie.
- `POST /api/v1/documents/process` — multipart field `file`, with the returned
  token in `X-CSRF-TOKEN` and the cookie preserved.
- `GET /api/v1/documents/{id}` — retrieves a saved result.
- `GET /health` — checks the database health.

GPT-6 Luna is the default. To run GPT-5.6 Luna for a local comparison, set the
environment variable before `dotnet run`:

```powershell
$env:OpenAI__Model = 'gpt-5.6-luna'
```

Clear the override after the run with
`Remove-Item Env:OpenAI__Model -ErrorAction SilentlyContinue`.

Provider selection is independent: set
`$env:DocumentAnalysis__Provider = 'openai'` to select the supported provider.
OpenAI is currently the only implemented provider.

## OpenAI model configuration and evaluation

Both model profiles are configured in
[`src/DocumentProcessor.API/appsettings.json`](src/DocumentProcessor.API/appsettings.json).
At the time these docs were checked, OpenAI lists both models as supporting the
Responses API, image input and Structured Outputs. Their published standard
short-context text rates per million tokens are:

| Model | Input | Cached input | Cache write | Output |
| --- | ---: | ---: | ---: | ---: |
| `gpt-5.6-luna` | $0.20 | $0.02 | $0.25 | $1.20 |
| `gpt-6-luna` | $0.10 | $0.01 | $0.125 | $0.50 |

Both profiles include the published long-context rates for prompts above
272,000 input tokens. The UI cost is a local estimate using the configured
rates and response usage counters; it is not the provider invoice, and account,
region, processing mode or pricing changes can affect it.

The model pages document Responses, image input and Structured Outputs for
[GPT-5.6 Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna) and
[GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna). Current
rates are published on the [OpenAI API pricing page](https://developers.openai.com/api/docs/pricing).
The live comparison on 2026-10-21 processed five synthetic documents with each
model. Both returned the configured model ID and passed 108/108 exact checks
covering document classification, supplier/customer identity, dates, currency,
totals, review flags, line fields and required issue codes. GPT-6 Luna's median
latency was 3,602 ms versus 3,095 ms for GPT-5.6 Luna (16.4% slower). Estimated
cost was $0.001769 versus $0.003601 per five-document set (50.9% lower for
GPT-6 Luna). Both models correctly flagged the inconsistent invoice and the
unknown purchase order for review. The sample is too small to claim general
quality superiority; GPT-6 Luna is the default because it matched extraction
accuracy on this set at lower estimated cost, with higher latency. See the
[model evaluation report](docs/MODEL-EVALUATION-2026-10-21.md) and its raw
[results](artifacts/model-evaluation-2026-10-21/results.json).

The integration sends PDF bytes as a PDF input and images as image input, uses
strict JSON Schema Structured Outputs, disables stored responses, and sets
reasoning effort to `none`. OpenAI document input behavior is described in the
[File inputs guide](https://developers.openai.com/api/docs/guides/file-inputs).

## Persistence, privacy and security boundaries

The application stores document metadata, a SHA-256 digest, processing status,
validated structured analysis and processing telemetry in PostgreSQL. It does
not persist the uploaded PDF/image bytes. The provider request uses a generic
PDF filename. Application logs omit original filenames, document content,
extracted values, API keys and raw provider exception messages.

The API enforces a 20 MiB limit, supported media types and file signatures.
Antiforgery protects browser form posts, but it is not user authentication.
This is a local portfolio demo: do not expose it to an untrusted network or
store real customer documents without adding authentication, authorization,
retention/deletion rules, appropriate encryption and operational controls.

## Verification status

The Release solution builds with zero warnings/errors and 57/57 automated tests
pass. Node syntax checks and the local NuGet vulnerability audit passed. Docker
PostgreSQL is healthy; migrations and 10 successful synthetic document writes
were verified, with one failed database record from the earlier local image
MIME construction bug. After the fix, PNG, JPEG and WebP request construction
passes regression tests. The browser displayed a saved inconsistent-invoice
result, review warnings and provider telemetry with no console errors or
warnings. The form handler was also exercised with mocked fetch responses to
verify the antiforgery header, multipart file field and result rendering. The
live OpenAI comparison used exactly 10 completed provider
responses; the extra upload attempt failed locally before any provider request
and produced no usage tokens. API compatibility and extraction have therefore
been checked with both configured models, but acceptance remains limited to
the synthetic evaluation set. See [docs/DEMO-CHECKLIST.md](docs/DEMO-CHECKLIST.md),
[docs/HANDOFF_ES.md](docs/HANDOFF_ES.md) and the
[evaluation report](docs/MODEL-EVALUATION-2026-10-21.md).

GitHub Actions runs restore, Release build, the full test suite and a transitive
NuGet vulnerability audit on pull requests and pushes to `main`. The workflow
does not require OpenAI credentials, PostgreSQL or billable provider calls.

## License

Project-owned source code and documentation are licensed under the MIT License;
see [LICENSE](LICENSE) for the 2026 copyright notice. Third-party dependencies
retain their own terms. The test projects use native xUnit assertions and do
not depend on FluentAssertions.

The application pins MediatR 12.5.0, whose package metadata declares
[Apache-2.0](https://packages.nuget.org/packages/MediatR/12.5.0). The root MIT
license applies to project-owned work and does not relicense MediatR. The
upstream maintainer states that MediatR 12 and earlier receive no support
guarantees, bug fixes or security fixes; this is a maintenance risk to accept
and monitor, not a license requirement. MediatR 12.5.0 targets .NET 6 and .NET
Standard 2.0 and is compatible with this .NET 10 application. The restored
dependency graph contains MediatR 12.5.0 and MediatR.Contracts 2.0.1 only; no
MediatR 13/14 package is present. See the
[maintainer's support statement](https://github.com/LuckyPennySoftware/MediatR/discussions/1123)
and the [public release readiness audit](docs/PUBLIC-RELEASE-READINESS.md).

## Out of scope

Email ingestion, n8n, ERP integrations, workflow builders and unrelated
portfolio cases are intentionally excluded.
