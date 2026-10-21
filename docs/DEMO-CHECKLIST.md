# Case 2 demo checklist

## Implemented and covered locally

- [x] Clean dependency direction, document aggregate lifecycle and CQRS use cases.
- [x] FluentValidation at the application boundary.
- [x] OpenAI adapter behind the application analyzer port.
- [x] Strict JSON Schema parsing and rejection of malformed/unexpected output.
- [x] PDF/image upload, 20 MiB boundary, supported MIME types and file signatures.
- [x] Deterministic finance checks for lines, discounts, VAT, surcharges,
  withholdings, currency precision and totals.
- [x] Human-review status when extraction is incomplete, ambiguous, warns or
  fails deterministic checks.
- [x] PostgreSQL persistence implementation; uploaded document bytes are not
  persisted.
- [x] Provider latency, token, cached-token, cache-write-token and estimated
  cost telemetry.
- [x] Browser demo UI with antiforgery token, result breakdown and review state.
- [x] Synthetic unit and API TestServer coverage using fake provider/repository
  dependencies where appropriate.
- [x] Release build and tests passed: 57/57 tests; Node syntax check passed;
  local NuGet vulnerability audit reported no vulnerable packages.
- [x] Local browser smoke check: page and antiforgery endpoint returned HTTP
  200; browser UI rendered without console errors or warnings.
- [x] Docker PostgreSQL started, Development migration applied, and `/health`
  returned HTTP 200.
- [x] Ten synthetic documents were processed and persisted with the real API;
  the database contains 10 `Completed` and one earlier `Failed` local request.
- [x] A saved inconsistent-invoice result was loaded through the read endpoint
  and rendered in the browser with its review warning and telemetry; no browser
  console errors or warnings.
- [x] Browser form handler smoke-tested with mocked fetch: it requests an
  antiforgery token, sends a multipart `file` field and renders a completed
  response. The mock avoided an extra provider request; the same API upload
  endpoint and persistence were exercised by the ten-request comparison.
- [x] Compared both live models on five synthetic files each. Each passed
  108/108 exact ground-truth checks. See
  [`MODEL-EVALUATION-2026-10-21.md`](MODEL-EVALUATION-2026-10-21.md).

## Still needs environment/provider acceptance

- [ ] Record a 45–90 second demo after those checks pass.
- [ ] Add authentication/authorization and customer-data retention controls
  before exposing the app beyond a trusted local machine.

## Verification boundaries

The 57 automated tests remain isolated from live services. The ten successful
OpenAI responses and database writes are local acceptance evidence, not proof
of performance on customer documents. One image submission failed before the
provider call because the SDK requires an image MIME type on `BinaryData`; the
code was fixed and PNG/JPEG/WebP regression tests now pass. The extra failed
document row has no provider usage. `gpt-6-luna` is the default based on equal
accuracy and lower estimated cost in this small set, despite higher measured
latency. Antiforgery does not replace user authentication.

See [HANDOFF_ES.md](HANDOFF_ES.md) for the PowerShell startup commands and
current handoff details.
