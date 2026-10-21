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
- Replaced MediatR 14.1.0 with MediatR 12.5.0. The CQRS handlers, assembly
  scanning and validation pipeline behavior remain unchanged.
- Updated the dependency license assessment and documented the upstream
  support limitation for MediatR 12 and earlier.

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
- MediatR 12.5.0's NuGet package metadata declares Apache-2.0 and lists .NET 6
  and .NET Standard 2.0 targets, compatible with this project's `net10.0`
  target. The upstream maintainer identifies v12 and earlier as unsupported:
  no support guarantees, bug fixes or security fixes are provided. This is a
  maintenance/security-update risk, not a license blocker. The restored graph
  contains MediatR 12.5.0 and MediatR.Contracts 2.0.1 only; no MediatR 13/14
  package is present. FluentAssertions is absent from test references; native
  xUnit assertions remain.
- License metadata was rechecked after restore. MediatR 12.5.0 declares
  Apache-2.0; the project-owned source remains MIT. No MediatR-specific license
  blocker to publishing this source repository was identified. This is a
  package-metadata review, not a legal opinion.
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

- [ ] Accept and monitor the maintenance risk that MediatR 12.5.0 receives no
  upstream support guarantees, bug fixes or security fixes.
- [ ] Confirm that the human-readable author name in the initial commit is
  intended to be public. Changing existing commit metadata requires a
  coordinated history rewrite, not a normal pull request.
- [ ] Review GitHub secret scanning, push protection and dependency security
  settings; the repository API returned no security-and-analysis state.
- [ ] Run the managed repository security scan after the filesystem runner
  permission issue is resolved.
- [ ] Keep the demo local until authentication, authorization and customer-data
  lifecycle controls are in place.
- [ ] Record a short demo using only synthetic documents and recheck every
  claim in the model evaluation before using it in sales material.

## Licensing references

- [MIT License — Open Source Initiative](https://opensource.org/license/mit)
- [MediatR 12.5.0 NuGet package metadata and license](https://packages.nuget.org/packages/MediatR/12.5.0)
- [Upstream maintainer statement on Apache licensing and v12 support](https://github.com/LuckyPennySoftware/MediatR/discussions/1123)
