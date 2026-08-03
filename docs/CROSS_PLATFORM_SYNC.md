# SpectraGrab Cross-Platform Sync Contract

Product family:

- Windows: `johngraven75/SpectraGrab`
- Android: `johngraven75/SpectraGrab-Android`
- iOS: `johngraven75/SpectraGrab-iOS`

## Source of truth

Windows is the feature-definition reference. Android and iOS must implement accepted Windows feature changes unless the platform makes a capability impossible or materially different. Any exception must be documented in the parity matrix and release notes.

## Required synchronized capability areas

- URL inspection and metadata
- provider-specific and generic extraction paths
- direct media
- HLS and DASH
- playlists
- bounded crawl/discovery fallback
- queue management
- pause/cancel/resume
- retries and robust process/network error handling
- format/quality selection
- persistent settings
- live-stream capture with progress and safe stop/finalization
- versioned provider and add-in JSON configs that preserve user settings across upgrades
- human CAPTCHA/browser handoff
- authorized-session handling where platform APIs permit
- release/version metadata

## Release parity gate

A coordinated release is green only when Windows, Android, and iOS share the intended product version and their required CI/build/package gates pass. Store/signing infrastructure blockers must be explicitly identified; they must never be disguised as successful publication.

## Safety boundary

No DRM bypass, paywall/access-control circumvention, credential bypass, or automated CAPTCHA solving/bypass.

Credentials remain in platform-secure storage or environment variables and are never persisted in integration JSON.
