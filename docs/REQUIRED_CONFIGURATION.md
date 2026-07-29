# Required iOS Configuration

## Toolchain

- macOS build host
- supported Xcode selected for the target SDK
- .NET 8 SDK
- .NET MAUI iOS workload (`dotnet workload install maui-ios`)
- Git / GitHub Actions

CI is defined in `.github/workflows/ios-ci.yml` and performs an unsigned Release simulator validation build. Device archive/IPA/TestFlight publication requires Apple signing and App Store Connect configuration.

## Product settings

- Application ID: `com.johngraven75.spectragrab`
- Display title: `SpectraGrab`
- Version source: `ApplicationDisplayVersion` + `ApplicationVersion` in `SpectraGrab.iOS.csproj`
- Windows reference repository: `johngraven75/SpectraGrab`
- Android peer repository: `johngraven75/SpectraGrab-Android`

## Required signing/publication secrets

Configure repository/environment secrets; never commit them:

- `APPLE_CERTIFICATE_P12`
- `APPLE_CERTIFICATE_PASSWORD`
- `APPLE_PROVISIONING_PROFILE`
- `APP_STORE_CONNECT_API_KEY`
- `APP_STORE_CONNECT_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`

Production archive/export/TestFlight/App Store publication is not considered complete until signing and App Store Connect delivery succeed.

## Automation rule

Any failed build/package/parity gate must be diagnosed, repaired, and rerun. Do not remove a feature to obtain a green build.
