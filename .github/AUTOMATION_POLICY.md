# Total Automation Policy

This repository follows the owner's repo-wide automation rules plus `REPO_RULES.md` and `docs/CROSS_PLATFORM_SYNC.md`.

- Preserve accepted functionality and Windows/Android parity.
- Inspect failures, fix root causes, commit, and rerun until green or until a hard external blocker is proven.
- Require clean restore/build validation and current iOS artifacts.
- Treat compile success as necessary but not sufficient: verify runtime/UI-critical paths and persistent settings/queue state.
- Retain build artifacts and diagnostics.
- Prefer automated build, test, packaging, release, cleanup, maintenance, and parity checks.
- Never report green without current evidence for the current commit.
- App Store/TestFlight publication requires valid Apple signing/App Store Connect credentials.
- Never commit signing keys, tokens, credentials, certificates, or provisioning profiles.
