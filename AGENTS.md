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

## Forward-thinking engineering standard

## Forward-thinking implementation standard

All human and AI-assisted codework in this repository must be forward-thinking, innovative, effective, and precise.

### Purpose and architecture

- Begin every implementation with a clear purpose, intended user outcome, constraints, and measurable completion criteria.
- Organize implementation and reporting into explicit **Front end**, **Connector / integration**, and **Back end** sections when those layers apply.
- Define contracts between layers before implementation. Keep presentation, transport/integration, and domain logic separated.
- If a layer does not apply, state that explicitly instead of adding unnecessary architecture.

### Implementation quality

- Design for maintainability, security, performance, accessibility, observability, and future upgrades.
- Prefer cohesive root-cause solutions over piecemeal patches.
- Innovation must provide practical value and must not introduce avoidable complexity or regressions.
- Use repository-specific, freshly reasoned code. Do not submit generic “big-box” code walls, placeholders, fabricated integrations, or unreviewed boilerplate.
- Preserve accepted functionality unless removal is explicitly approved and documented.

### Validation and publication gate

- Define the validation plan before publication.
- Run the relevant formatting, static analysis, type checks, unit tests, integration tests, packaging checks, and user-flow tests supported by the repository.
- Test cross-layer behavior whenever front end, connector, and back end interact.
- Do not publish, release, merge, or describe work as complete while required checks fail.
- If full validation is blocked, identify the exact blocker and leave the work explicitly incomplete; do not imply success.
- Never weaken security controls or remove meaningful tests merely to obtain a green build.

### Required completion report

Every completed code task must report:

1. **Purpose** — the problem solved and intended outcome.
2. **Front end** — user-visible changes and validation, or “not applicable.”
3. **Connector / integration** — APIs, IPC, storage, provider, or platform wiring and validation, or “not applicable.”
4. **Back end** — domain logic, services, persistence, and validation, or “not applicable.”
5. **Completion** — files/components changed, tests run, results, remaining risks, and publication/release status.
