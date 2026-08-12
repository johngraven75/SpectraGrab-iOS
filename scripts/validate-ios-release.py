#!/usr/bin/env python3
import json
import pathlib
import xml.etree.ElementTree as ET

root = pathlib.Path(__file__).resolve().parent.parent
project = ET.parse(root / "SpectraGrab.iOS.csproj").getroot()


def property_value(name: str) -> str:
    node = project.find(f".//{name}")
    return "" if node is None or node.text is None else node.text.strip()


if property_value("ApplicationDisplayVersion") != "0.3.0":
    raise SystemExit("ApplicationDisplayVersion must be 0.3.0")
if property_value("ApplicationVersion") != "3":
    raise SystemExit("ApplicationVersion must be the monotonically increasing value 3")

expected = {
    "providers": {"huggingface", "theporndb", "stashdb", "extractor-metadata", "extractor-thumbnail"},
    "plugins": {"emby", "jellyfin", "plex", "localai", "quickconnect"},
}
for kind, expected_ids in expected.items():
    directory = root / "ConfigDefaults" / kind
    actual_ids = {path.stem for path in directory.glob("*.json")}
    if actual_ids != expected_ids:
        raise SystemExit(f"{kind} config set mismatch: {sorted(actual_ids)}")
    for path in directory.glob("*.json"):
        config = json.loads(path.read_text(encoding="utf-8-sig"))
        if config.get("id") != path.stem or config.get("schemaVersion", 0) < 2 or config.get("version", 0) < 2:
            raise SystemExit(f"Invalid versioned config: {path}")
        if not isinstance(config.get("settings"), dict):
            raise SystemExit(f"Config settings must be an object: {path}")

workflow = (root / ".github/workflows/ios-ci.yml").read_text(encoding="utf-8")
for required in (
    "actions/checkout@v7",
    "actions/setup-dotnet@v6",
    "actions/upload-artifact@v7",
    "SpectraGrab-iOS-v${RELEASE_VERSION}-Simulator-TEST.zip",
    "iossimulator-arm64",
    "Xcode_15.4.app",
):
    if required not in workflow:
        raise SystemExit(f"iOS workflow is missing: {required}")

main_page = (root / "MainPage.xaml").read_text(encoding="utf-8")
download_service = (root / "Services/MobileAutomatedDownloadService.cs").read_text(encoding="utf-8")
capture_service = (root / "Services/MobileLiveCaptureService.cs").read_text(encoding="utf-8")
persistent_service = (root / "Services/MobilePersistentConfigService.cs").read_text(encoding="utf-8")
if "Stop Download" not in main_page or "Stop &amp; Finalize" not in main_page:
    raise SystemExit("iOS download and Live Capture stop controls are required")
if ".partial" not in download_service or "DeleteIfExists" not in download_service:
    raise SystemExit("iOS automated downloads must use atomic partial-file cleanup")
if ".partial" not in capture_service or "Stopped and finalized" not in capture_service:
    raise SystemExit("iOS Live Capture must finalize user-stopped captures atomically")
if "AtomicWriteAsync" not in persistent_service:
    raise SystemExit("iOS integration configuration writes must remain atomic")

print("Verified iOS 0.3.0 (build 3), config parity, cancellation/cleanup, Live Capture, and Simulator TEST packaging.")