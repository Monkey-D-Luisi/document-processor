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
- Added the license and history risks below to the README and handoff.

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
- The root repository currently has no project-level `LICENSE`. GitHub says
  that a public repository may be viewed and forked under its Terms of Service,
  but without a project license the default copyright rules retain the owner's
  rights and do not grant general reuse or redistribution permission. The
  owner must choose the intended license before describing this as open source.
- MediatR 14.1.0 is within the versions covered by Lucky Penny Software's
  current license agreement. The package includes an RPL-1.5 source-license
  path and a separate commercial-license path. Its agreement also defines
  distribution rights broadly, including making public software that relies on
  the library. Confirm that the chosen project license and intended public
  source distribution are compatible with the selected MediatR license. For
  commercial production or client work, the end client's eligibility and
  license path may matter. This branch retains MediatR to preserve the agreed
  CQRS implementation; it does not resolve this owner-level licensing choice.
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

- [ ] Choose the project's license and add the matching root `LICENSE` file.
- [ ] Confirm the MediatR 14.1 licensing route is compatible with that choice
  and any intended source distribution. Alternatives such as using a
  pre-commercial MediatR release require a separate compatibility and support
  review.
- [ ] Confirm that the human-readable author name in the initial commit is
  intended to be public. Changing existing commit metadata requires a
  coordinated history rewrite, not a normal pull request.
- [ ] Review GitHub secret scanning, push protection and dependency security
  settings for this repository.
- [ ] Run the managed repository security scan after the filesystem runner
  permission issue is resolved.
- [ ] Keep the demo local until authentication, authorization and customer-data
  lifecycle controls are in place.
- [ ] Record a short demo using only synthetic documents and recheck every
  claim in the model evaluation before using it in sales material.

## Licensing references

- [FluentAssertions license](https://github.com/fluentassertions/fluentassertions/blob/main/LICENSE)
- [MediatR license agreement](https://luckypennysoftware.com/license)
- [MediatR licensing FAQ](https://luckypennysoftware.com/faq)
- [GitHub guidance on repository licensing](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)
