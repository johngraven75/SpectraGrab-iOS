# SpectraGrab Repository Rules

These rules apply to the entire repository and to coordinated SpectraGrab work across Windows, Android, and iOS.

## Non-negotiable product rules

1. No regressions. Existing accepted features, functions, options, and visible destinations must remain available unless explicitly approved for removal.
2. Windows SpectraGrab is the feature-definition reference. Android and iOS must track it in parity.
3. Every defect found on one platform must be audited against the other two platforms and receive regression coverage where applicable.
4. Settings and persistent state must survive builds/upgrades and remain semantically consistent across operating systems where platform APIs permit.
5. Do not cap or silently truncate user-visible library/media collections unless a documented technical limit requires pagination or virtualization.
6. Use fresh, maintainable code for repairs. Do not layer brittle temporary patches over broken behavior.

## Build and CI rules

1. Clean restore/build is mandatory.
2. Any workflow or build failure must be investigated, repaired, committed, and rerun until green or until a hard external blocker is demonstrated.
3. CI must retain useful diagnostics and build artifacts.
4. Runtime/UI-critical behavior must be exercised, not inferred only from compilation.
5. Installer/package validation happens only after functional verification.
6. A platform is not "done" merely because code compiles.

## Release gate

Do not call a release complete until the platform has:

- clean build;
- parity/regression verification;
- runtime/UI verification for critical paths;
- persistent-state/settings verification;
- production package artifact;
- release notes/change record;
- published GitHub release when release publication is requested;
- no unresolved release-blocking regressions.

Coordinated releases require Windows + Android + iOS green, unless a hard external signing/store/infrastructure blocker is explicitly documented.

## Automation and maintenance

- Prefer total automation for restore, validation, package, artifact retention, release creation, and routine repository maintenance.
- Workflows must have robust triggers and clear failure diagnostics.
- Keep repository layout professional and organized.
- Keep generated/build outputs out of source control unless intentionally versioned.
- Track version/parity metadata in source control.

## Cross-platform downloader contract

Support public media and media the user is authorized to access, including direct media, HLS/DASH, playlists, provider/generic extraction, retries, bounded discovery/crawl, queue controls, format/quality selection, and legitimate authenticated-session handling where the platform permits.

Do not implement DRM bypass, paywall/access-control circumvention, credential bypass, or automated CAPTCHA solving/bypass. Human verification handoff is allowed.
