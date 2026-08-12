# Repository Engineering Standard

Mandatory for all AI-assisted engineering. Before code define product goal, target user, success criteria, non-goals, assumptions, missing requirements, and architecture. Organize work as **Frontend**, **Connector / integration**, and **Backend** where applicable. Use independently testable vertical slices/worktrees with exact files, interfaces, error handling, observability, security implications, and definition of done.

Ship fresh idiomatic production code using SOLID, DRY, explicit types, immutable data where practical, dependency injection, secure defaults, validation, null safety, cancellation/disposal, and clear separation of concerns. No TODOs, placeholders, fabricated integrations, credentials/signing secrets, or incomplete production paths. Preserve backward compatibility unless migration is explicitly approved.

Every slice requires appropriate unit, real OS/filesystem integration, contract, security, performance-budget, and manual UI/install/upgrade/distribution tests. Run all repository lint/static/type/unit/integration/E2E/archive/package validation. Self-review races, deadlocks, leaks, disposal, retries, lifecycle, permissions, interrupted operations and rollback before completion.

Production readiness requires diff summary, changelog/release notes, migration/install notes, API/config/environment docs, rollback plan, monitoring plan, test evidence and artifact checksums/signing evidence. Atomic conventional commits only; commit before builds; never merge around failing required checks or claim unverified completion.
