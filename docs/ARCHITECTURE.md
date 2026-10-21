# Architecture

## Goal

This demo proves a reusable backend pattern:

```text
PDF / image
    |
    v
API boundary
    |
    v
Application use case
    |
    +--> Document aggregate lifecycle
    |
    +--> IDocumentAnalyzer port
             |
             v
        IDocumentAnalysisProviderFactory
             |
             v
        IDocumentAnalysisProvider
             |
             v
        OpenAI Responses API
        + file/image input
        + strict JSON Schema
             |
             v
     untrusted extraction candidate
             |
             v
 deterministic application policy
             |
             v
 validated domain analysis
             |
             v
         PostgreSQL
```

## Dependency direction

```text
API ------------> Application ------------> Domain
  \                   ^
   \                  |
    ----> Infrastructure
              |
              +-------> Application
              +-------> Domain
```

- `Domain` has no external package dependencies.
- `Application` owns use cases, the `IDocumentAnalyzer` and provider ports,
  provider factory, validation and deterministic analysis policy.
- `Infrastructure` implements persistence, IoC scanning and the OpenAI provider.
- `API` owns HTTP concerns only.
- Raw uploaded document bytes are **not persisted**.
- Model output is not trusted as domain state until deterministic validation is applied.

## Why the OpenAI output is a candidate, not a domain object

Structured Outputs constrain the shape of the response, but they do not prove that
the extracted values are semantically correct.

`DocumentAnalysisCandidate` is therefore an integration/application model.

`DocumentAnalysisPolicy` is the trust boundary that:

- parses dates;
- normalizes currency;
- checks expected fields by document type;
- checks line extensions, discounts, tax bases and amounts, surcharges and
  withholdings;
- checks subtotal, tax and payable-total arithmetic using currency minor units;
- converts model uncertainty into review issues.

Only after that step is a `DocumentAnalysis` attached to the aggregate.

## Human review

The demo deliberately avoids an invented "AI confidence percentage".

`RequiresReview` is derived from observable signals:

- unknown document type;
- missing expected fields;
- model-declared uncertainty;
- model warnings;
- deterministic arithmetic inconsistencies.

This is easier to explain and safer than treating self-reported model confidence
as a calibrated probability.

## Provider boundary and cost telemetry

`DocumentAnalysisProviderType` is a stable typed identity whose external ID is
`openai`. `DocumentAnalysis:Provider` selects that identity independently from
`OpenAI:Model`. Configuration is validated at startup; only the OpenAI
provider is implemented. The Application use case continues to depend only on
`IDocumentAnalyzer`.

When another provider is added, make OpenAI options validation and SDK client
construction conditional on selecting `openai`. The current host validates
OpenAI configuration at startup even when a different provider is selected;
that should not become a requirement for other providers.

The Infrastructure adapter sends PDF/image input through the Responses API and
requests a strict JSON Schema result. Provider output is parsed as a candidate;
it does not bypass application policy. Provider failures are mapped to stable
failure kinds, and logs avoid source filenames, document contents, extracted
business values, API keys and raw exception messages.

Both `gpt-5.6-luna` and `gpt-6-luna` have configuration profiles. `gpt-6-luna`
is the default after a five-document synthetic comparison returned 108/108
exact checks for each model. It had 50.9% lower estimated cost and 16.4% higher
median latency in that small sample. This does not establish general quality
superiority. The reported USD amount is an estimate based on response token
counters and rates in `appsettings.json`, including cached and cache-write
tokens and the configured long-context threshold. It can differ from the final
provider charge if the pricing configuration, region or account terms change;
see [`MODEL-EVALUATION-2026-10-21.md`](MODEL-EVALUATION-2026-10-21.md).

## Verification boundary

Unit and API TestServer tests use synthetic documents and fake provider or
repository dependencies. PostgreSQL migrations, persistence and both live
model integrations were also exercised locally with synthetic documents.
Results are limited to the small evaluation set; see
[`DEMO-CHECKLIST.md`](DEMO-CHECKLIST.md).

The GitHub Actions workflow runs .NET 10 restore, Release build, the full test
suite and a NuGet vulnerability audit on pull requests and pushes to `main`.
It does not use OpenAI credentials or make provider calls.

The UI uses antiforgery for browser uploads, but the demo has no user
authentication or authorization. Keep it on a trusted local machine until
those access controls and customer-data retention requirements are addressed.

## Production extensions

The current synchronous HTTP flow is deliberate for a portfolio demo.

For high-volume production, the same use case can evolve to:

```text
upload -> persist metadata -> queue -> worker -> IDocumentAnalyzer -> persist result
```

without moving OpenAI concerns into the domain or API layers.

Other likely production additions:

- tenant/auth boundary;
- encrypted object storage when raw documents must be retained;
- malware scanning before persistence;
- idempotency policy chosen per client workflow;
- retry policy for transient provider errors;
- retention/deletion policy;
- audit trail;
- ERP/CRM connector;
- manual-review queue;
- field-level correction history.
