# Public release readiness

Audit date: 2026-10-21. Scope: the repository's tracked source, documentation,
configuration examples, synthetic evaluation artifacts, and available Git
history. This report records source inspection and local checks; it is not a
legal opinion or a managed security-scan report.

## Changes in this branch

- Replaced FluentAssertions in all four test projects with equivalent native
  xUnit assertions and removed the package references. FluentAssertions 8's
  community license is limited to non-commercial use; other use requires a
  paid commercial license.
- Extended `.gitignore` for local secret stores, local overrides, temporary
  files and backups. Synthetic evaluation PDFs, images, ground truth and
  results remain tracked intentionally.
- Added the 2026 MIT license for project-owned code, naming `Monkey-D-Luisi` as
  the copyright holder, and updated README and handoff with the owner's final
  license decisions.

## Audit findings

- The original repository tree had 87 tracked files and eight project files
  (four application projects and four test projects); this branch adds this
  audit document. No build output, database files, local environment files or
  usable OpenAI credentials are tracked.
- The published history contains one initial commit. The additional Codex
  capture reference points to a tree identical to that commit; no unreachable
  Git objects were found. Tracked-text searches found no API key/token patterns,
  private Windows user paths or the local account identifier. The initial
  commit has a human-readable author name and a GitHub `noreply` email address;
  the owner should confirm the displayed name is intended for public release.
- Evaluation documents and output are synthetic. The checked-in OpenAI key
  setting is empty. `appsettings.json` and Compose contain the same fixed
  development-only PostgreSQL credential so the local demo works out of the
  box. Compose binds its database port to `127.0.0.1`; never reuse that
  credential or expose the demo outside a trusted local machine. The PDFs have
  no populated author/title metadata; the PNG and JPEG contain no EXIF, XMP or
  text metadata markers.
- The root `LICENSE` now contains the standard MIT text with `Copyright (c)
  2026 Monkey-D-Luisi`. It licenses project-owned code and documentation; it
  does not replace the licenses of NuGet dependencies.
- MediatR 14.1.0 is within Lucky Penny Software's current agreement, which
  applies to MediatR 13.0.0 and later. Its upstream repository offers source
  under RPL-1.5; the separate commercial agreement grants distribution rights
  to a named customer and expressly excludes redistribution of MediatR source
  under that agreement. The agreement also defines “Distribution Rights” to
  include making public software that links to or relies on MediatR. The
  vendor's Community License is a contractual eligibility path, not a license
  that changes the package to MIT or grants source rights beyond the agreement.
  Based on these terms, publishing this dependent source repository under MIT
  is not established as permitted. Treat this as a real pre-publication
  blocker: obtain written confirmation from Lucky Penny Software for this
  repository's MIT source publication or resolve the conflicting terms before
  changing repository visibility. This is a compatibility review, not legal
  advice.
- Community eligibility requires annual revenue below USD 5 million (or
  non-profit budget below that threshold), no more than USD 10 million in
  outside capital, and excludes government/quasi-government entities and
  university operational software. For service-provider/client work, the end
  client must qualify. Eligible customers register through the vendor's
  pricing/account flow and obtain a key. Per the vendor FAQ, non-production
  development, CI, staging and QA do not require a key; production deployments
  do. MediatR 14.1.0 accepts the key through `cfg.LicenseKey` at registration.
  This demo does not currently wire a production key from configuration.
  FluentAssertions is absent from the test project references; native xUnit
  assertions remain in place as requested.
- License metadata was checked for the 92 distinct packages in the restored
  dependency graph. It identifies MIT, Apache-2.0, BSD-3-Clause and PostgreSQL
  licenses, plus MediatR's RPL-1.5 terms. The older `xunit.abstractions` package
  uses a legacy license URL that resolves to the xUnit repository's Apache-2.0
  license, with a separate MIT notice for imported code. No other restrictive
  package licenses were surfaced by this metadata review.
- The GitHub repository API returned `security_and_analysis: null`, so this
  audit could not confirm whether secret scanning, push protection or
  dependency security features are enabled.
- The app is a local demo without user authentication or authorization.
  Antiforgery protects browser uploads from CSRF; it does not control access.
  Keep the service local until access control, retention/deletion and other
  customer-data controls are implemented.

## Security coverage and limits

The local audit checked tracked file paths and text for common secret patterns,
private Windows paths and the local account identifier, reviewed the single
commit plus the capture tree, checked ignore behavior for representative secret
and build paths, and ran NuGet's vulnerability audit. The package audit found
no reported vulnerable packages in the restored dependency graph.

The managed Codex Security Deep Scan did not start: its launcher reported that
the parent task has no managed filesystem permission profile for a read-only
worker. Daybreak access was also unavailable, but that check is advisory. The
manual checks above do not replace the managed scan; repeat it when the
required runner profile is available. No provider calls or deployments were
performed for this audit.

## Checklist before making the repository public

- [ ] Resolve the MediatR 14.1.0 source-distribution compatibility blocker in
  writing with Lucky Penny Software, or make a new owner decision about the
  project/dependency licensing combination. Do not treat the project MIT file
  as granting rights to MediatR.
- [ ] Confirm that the human-readable author name in the initial commit is
  intended to be public. Changing existing commit metadata requires a
  coordinated history rewrite, not a normal pull request.
- [ ] Review GitHub secret scanning, push protection and dependency security
  settings; the repository API returned no security-and-analysis state.
- [ ] Run the managed repository security scan after the filesystem runner
  permission issue is resolved.
- [ ] If production use is planned, verify Community eligibility and connect
  the MediatR 14.1.0 `cfg.LicenseKey` setting to a deployment secret store.
- [ ] Keep the demo local until authentication, authorization and customer-data
  lifecycle controls are in place.
- [ ] Record a short demo using only synthetic documents and recheck every
  claim in the model evaluation before using it in sales material.

## Licensing references

- [MIT License — Open Source Initiative](https://opensource.org/license/mit)
- [MediatR 14.1.0 license notice](https://github.com/LuckyPennySoftware/MediatR/blob/v14.1.0/LICENSE.md)
- [MediatR 14.1.0 license-key configuration](https://github.com/LuckyPennySoftware/MediatR/blob/v14.1.0/README.md)
- [MediatR license agreement](https://luckypennysoftware.com/license)
- [MediatR licensing FAQ](https://luckypennysoftware.com/faq)
- [RPL-1.5 — Open Source Initiative](https://opensource.org/license/rpl-1-5)
