# Handoff — Case 2: Document Processor

## Current status

The application builds and has been tested locally with PostgreSQL and both configured OpenAI models. The default is **gpt-6-luna**; **gpt-5.6-luna** remains configurable. The comparison used synthetic documents. Provider selection is independent through DocumentAnalysis:Provider; **openai** is the only implemented provider. The source repository is public; the application has not been deployed.

Dependabot alerts, Dependabot security updates, secret scanning and push protection are enabled on GitHub. The owner reviewed and approved the human-readable author name on the merge commits for public display.

Future improvement: when adding another provider, make OpenAI option validation and OpenAI SDK client construction conditional on selecting **openai**, so other providers do not require OpenAI configuration.

Project-owned code and documentation use the MIT License, copyright 2026 Monkey-D-Luisi. MediatR 12.5.0 has its own Apache-2.0 license; the project MIT license does not replace it. No license blocker for publishing the project source was identified. MediatR 12 and earlier do not receive upstream support guarantees, bug fixes or security fixes, so this version requires ongoing maintenance-risk monitoring.

## Verification performed on October 21, 2026

- dotnet restore .\DocumentProcessor.sln: passed.
- dotnet build .\DocumentProcessor.sln --configuration Release: passed with zero warnings and errors.
- dotnet test .\DocumentProcessor.sln --configuration Release: 57 tests passed.
- The provider composition test starts the HTTP host in the Testing environment, requests an antiforgery token, and resolves IDocumentAnalyzer → ConfiguredDocumentAnalyzer → factory → OpenAiDocumentAnalyzer using a placeholder key. It makes no OpenAI request.
- node --check .\src\DocumentProcessor.API\wwwroot\app.js: passed.
- dotnet list .\DocumentProcessor.sln package --vulnerable --include-transitive: no vulnerable packages found.
- MediatR 12.5.0 builds and passes tests on net10.0. Its package declares .NET 6 and .NET Standard 2.0 compatibility. The restored dependency graph contains no MediatR 13 or 14 packages.
- The GitHub Actions workflow runs restore, Release build, tests and NuGet vulnerability auditing on pull requests and pushes to main. It needs no OpenAI secret and makes no provider calls.
- Docker PostgreSQL was healthy. The Development migration ran and /health returned HTTP 200. The local database contains 10 Completed documents and one Failed record from the first image-upload attempt.
- The first image upload failed locally because BinaryData was created without a MIME type. The request did not reach OpenAI and generated no tokens. The adapter was fixed and regression tests cover PNG, JPEG and WebP.
- Ten real OpenAI responses were completed, five per model, using the same five synthetic documents. The provider did not retry. The failed local upload explains why the API received 11 submissions.
- The UI displayed the saved inconsistent-invoice result from PostgreSQL, including its review warning, model, latency, tokens and estimated cost. The browser had no console errors or warnings. The form handler was also tested with mocked network responses for antiforgery, multipart upload and result rendering without another provider call.
- Both models passed 108/108 exact ground-truth checks. The [model evaluation report](MODEL-EVALUATION-2026-10-21.md), synthetic documents, ground truth and anonymized-by-design responses are in the report and artifacts/model-evaluation-2026-10-21/.

The recorded cost is a local estimate based on returned token counts and configured rates; it is not an OpenAI billing statement.

## Model decision

| Model | Exact checks | Median latency | Estimated cost / 5 documents |
| --- | ---: | ---: | ---: |
| gpt-5.6-luna | 108/108 | 3,095 ms | $0.003601 |
| gpt-6-luna | 108/108 | 3,602 ms | $0.001769 |

GPT-6 Luna cost 50.9% less and had 16.4% higher median latency in this run. Five documents are too small a sample to establish general quality or intelligence superiority. GPT-6 Luna is the default because it matched the observed extraction results at a lower estimated cost, in line with the project owner's preference.

## Windows setup

Requirements: .NET 10 SDK and Docker Desktop. Run these commands in PowerShell from the repository root:

```powershell
docker compose up -d
dotnet restore .\DocumentProcessor.sln
dotnet build .\DocumentProcessor.sln --configuration Release
dotnet test .\DocumentProcessor.sln --configuration Release

$keyFile = Join-Path $env:USERPROFILE 'secrets\openai_api_key.txt'
$env:OpenAI__ApiKey = (Get-Content -Raw $keyFile).Trim()
Remove-Item Env:OpenAI__Model -ErrorAction SilentlyContinue
try {
    dotnet run --project .\src\DocumentProcessor.API\DocumentProcessor.API.csproj --urls http://127.0.0.1:5178
}
finally {
    Remove-Item Env:OpenAI__ApiKey -ErrorAction SilentlyContinue
    Remove-Item Env:OpenAI__Model -ErrorAction SilentlyContinue
}
```

Open http://127.0.0.1:5178. In Development, the API applies pending migrations at startup. Read the API key from an external file; do not copy it into the repository or print it. Each uploaded document may trigger a billable OpenAI request.

To use GPT-5.6 Luna for a session, set $env:OpenAI__Model = 'gpt-5.6-luna' before running dotnet. Both models and their rates are configured in [appsettings.json](../src/DocumentProcessor.API/appsettings.json).

Endpoints:

- GET /api/v1/documents/antiforgery
- POST /api/v1/documents/process
- GET /api/v1/documents/{id}
- GET /health

The UI handles antiforgery and displays extracted data, line items, consistency checks, review status and telemetry.

## Remaining before a customer-facing demo

1. Record a short demo with synthetic documents and review the flow from upload through results.
2. Repeat the model comparison on a larger, more representative dataset before making commercial claims about quality, speed or savings.
3. Add authentication, authorization, retention and deletion rules, and operational controls before exposing the application to third parties or processing real customer documents.
4. Continue monitoring the unsupported MediatR 12.5.0 dependency. If this risk becomes unacceptable, evaluate a maintained alternative before upgrading to MediatR 13 or later; review its license separately.

The [public release readiness report](PUBLIC-RELEASE-READINESS.md) records that the managed security scan could not start because the environment lacks the managed filesystem permission profile required by its read-only worker. Daybreak access was also unavailable. The project owner accepted this as a non-blocking limitation after the manual audit found no credentials or sensitive files.

Antiforgery protects browser uploads from CSRF; it does not authenticate users. Keep the API and read endpoint local until access controls are added.

Email ingestion, n8n and ERP integrations remain out of scope for Case 2.
