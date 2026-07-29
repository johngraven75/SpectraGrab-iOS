# Repository Agent / Automation Operating Rules

All automated or AI-assisted repository work must follow `REPO_RULES.md` and `docs/CROSS_PLATFORM_SYNC.md`.

## Required execution loop

1. Inspect the current repository state and existing workflows before changing code.
2. Preserve accepted functionality; do not remove features to make CI pass.
3. Implement the smallest maintainable correction that addresses root cause.
4. Run/trigger clean validation after each meaningful change.
5. If validation fails, inspect the actual failing step/log, repair it, and rerun.
6. Do not report green until the current commit has current green evidence.
7. Produce and retain release artifacts when packaging is part of the task.
8. Keep iOS parity with Windows and Android tracked explicitly.

## iOS toolchain baseline

- Git / GitHub Actions
- macOS runner
- supported Xcode selected explicitly
- Swift Package Manager and/or Xcode project/workspace resolution
- build + unit/UI tests where available
- Release archive generation
- IPA/export only when valid signing/export configuration exists
- App Store/TestFlight publishing only with valid Apple signing and App Store Connect credentials
- secrets only through GitHub Secrets/secure environment variables; never commit signing credentials

## Downloader/media baseline

Use supported Apple APIs/libraries for HTTP, HLS, background transfers, persistent queue state, and user-authorized session handling. DASH or extraction capabilities must use implementations allowed by the platform and distribution model. Do not implement DRM or access-control circumvention.
