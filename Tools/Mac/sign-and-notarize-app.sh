#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -lt 2 ]]; then
  echo "Usage: sign-and-notarize-app.sh <StringTheory.app> <output.zip>" >&2
  exit 2
fi

APP_BUNDLE="$1"
OUTPUT_ZIP="$2"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ENTITLEMENTS="${MACOS_ENTITLEMENTS_PATH:-${REPO_ROOT}/Tools/Mac/StringTheory.entitlements}"
INFO_PLIST="${APP_BUNDLE}/Contents/Info.plist"
DEFAULT_APP_IDENTIFIER="com.anthonysfeir.stringtheory"
APP_IDENTIFIER="${MACOS_APP_IDENTIFIER:-${DEFAULT_APP_IDENTIFIER}}"
IDENTITY="${DEVELOPER_ID_APPLICATION:-}"
APPLE_ID="${APPLE_ID:-}"
APPLE_TEAM_ID="${APPLE_TEAM_ID:-}"
APPLE_APP_SPECIFIC_PASSWORD="${APPLE_APP_SPECIFIC_PASSWORD:-}"
KEYCHAIN="${MACOS_SIGNING_KEYCHAIN:-}"

if [[ -z "${IDENTITY}" ]]; then
  echo "DEVELOPER_ID_APPLICATION is required." >&2
  exit 1
fi

if [[ -z "${APPLE_ID}" || -z "${APPLE_TEAM_ID}" || -z "${APPLE_APP_SPECIFIC_PASSWORD}" ]]; then
  echo "APPLE_ID, APPLE_TEAM_ID, and APPLE_APP_SPECIFIC_PASSWORD are required." >&2
  exit 1
fi

if [[ ! -d "${APP_BUNDLE}" ]]; then
  echo "App bundle not found: ${APP_BUNDLE}" >&2
  exit 1
fi

if [[ ! -f "${ENTITLEMENTS}" ]]; then
  echo "Entitlements file not found: ${ENTITLEMENTS}" >&2
  exit 1
fi

if [[ ! -f "${INFO_PLIST}" ]]; then
  echo "App Info.plist not found: ${INFO_PLIST}" >&2
  exit 1
fi

APP_IDENTIFIER="$(
  python3 - "${INFO_PLIST}" "${MACOS_APP_IDENTIFIER:-}" "${DEFAULT_APP_IDENTIFIER}" <<'PY'
import plistlib
import re
import sys

plist_path, requested_identifier, fallback_identifier = sys.argv[1:4]
identifier_pattern = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")

with open(plist_path, "rb") as handle:
    plist = plistlib.load(handle)

existing_identifier = str(plist.get("CFBundleIdentifier", "")).strip()
requested_identifier = requested_identifier.strip()
fallback_identifier = fallback_identifier.strip()

if requested_identifier:
    identifier = requested_identifier
elif identifier_pattern.match(existing_identifier):
    identifier = existing_identifier
else:
    identifier = fallback_identifier

if not identifier_pattern.match(identifier):
    raise SystemExit(f"Invalid macOS bundle identifier: {identifier}")

plist["CFBundleIdentifier"] = identifier
plist.setdefault("CFBundleExecutable", "StringTheory")
plist.setdefault("CFBundlePackageType", "APPL")

with open(plist_path, "wb") as handle:
    plistlib.dump(plist, handle, fmt=plistlib.FMT_XML, sort_keys=False)

print(identifier)
PY
)"

plutil -lint "${INFO_PLIST}"

KEYCHAIN_ARGS=()
if [[ -n "${KEYCHAIN}" ]]; then
  KEYCHAIN_ARGS=(--keychain "${KEYCHAIN}")
fi

codesign_nested_code() {
  local item="$1"
  echo "Signing ${item#${APP_BUNDLE}/}"
  codesign \
    --force \
    --timestamp \
    --options runtime \
    --sign "${IDENTITY}" \
    "${KEYCHAIN_ARGS[@]}" \
    "${item}"
}

codesign_app_bundle() {
  echo "Signing outer app bundle..."
  codesign \
    --force \
    --timestamp \
    --options runtime \
    --entitlements "${ENTITLEMENTS}" \
    --identifier "${APP_IDENTIFIER}" \
    --sign "${IDENTITY}" \
    "${KEYCHAIN_ARGS[@]}" \
    "${APP_BUNDLE}"
}

verify_app_bundle() {
  if codesign --verify --deep --strict --verbose=2 "${APP_BUNDLE}"; then
    return 0
  fi

  echo "Developer ID signature verification failed. Dumping signature diagnostics..." >&2
  codesign -dvvv "${APP_BUNDLE}" 2>&1 || true
  codesign -d --entitlements :- "${APP_BUNDLE}" 2>&1 || true
  codesign -d -r- "${APP_BUNDLE}" 2>&1 || true
  exit 1
}

is_macho_file() {
  local file_path="$1"
  file "${file_path}" | grep -Eq 'Mach-O|ar archive'
}

echo "Preparing ${APP_BUNDLE} for Developer ID signing..."
echo "Using app bundle identifier: ${APP_IDENTIFIER}"
xattr -dr com.apple.quarantine "${APP_BUNDLE}" 2>/dev/null || true

while IFS= read -r file_path; do
  if [[ "${file_path}" == "${APP_BUNDLE}/Contents/MacOS/"* ]]; then
    continue
  fi

  if is_macho_file "${file_path}"; then
    codesign_nested_code "${file_path}"
  fi
done < <(find "${APP_BUNDLE}" -type f -print | sort)

while IFS= read -r framework; do
  codesign_nested_code "${framework}"
done < <(find "${APP_BUNDLE}" -type d -name "*.framework" -print | sort)

while IFS= read -r bundle; do
  codesign_nested_code "${bundle}"
done < <(find "${APP_BUNDLE}" -type d \( -name "*.bundle" -o -name "*.plugin" \) -print | sort)

codesign_app_bundle

echo "Verifying Developer ID signature..."
verify_app_bundle

SUBMISSION_ZIP="${RUNNER_TEMP:-/tmp}/StringTheory-notary-submit.zip"
rm -f "${SUBMISSION_ZIP}" "${OUTPUT_ZIP}"
ditto -c -k --sequesterRsrc --keepParent "${APP_BUNDLE}" "${SUBMISSION_ZIP}"

echo "Submitting to Apple notarization..."
NOTARY_JSON="${RUNNER_TEMP:-/tmp}/StringTheory-notary-submit.json"
set +e
xcrun notarytool submit "${SUBMISSION_ZIP}" \
  --apple-id "${APPLE_ID}" \
  --team-id "${APPLE_TEAM_ID}" \
  --password "${APPLE_APP_SPECIFIC_PASSWORD}" \
  --wait \
  --output-format json > "${NOTARY_JSON}"
notary_exit=$?
set -e

cat "${NOTARY_JSON}"
notary_id="$(python3 - "${NOTARY_JSON}" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as handle:
    payload = json.load(handle)

print(payload.get("id", ""))
PY
)"
notary_status="$(python3 - "${NOTARY_JSON}" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as handle:
    payload = json.load(handle)

print(payload.get("status", ""))
PY
)"

if [[ -n "${notary_id}" && "${notary_status}" != "Accepted" ]]; then
  echo "Fetching Apple notarization log for ${notary_id}..."
  xcrun notarytool log "${notary_id}" \
    --apple-id "${APPLE_ID}" \
    --team-id "${APPLE_TEAM_ID}" \
    --password "${APPLE_APP_SPECIFIC_PASSWORD}" || true
fi

if [[ "${notary_exit}" -ne 0 || "${notary_status}" != "Accepted" ]]; then
  echo "Apple notarization failed with status '${notary_status}'." >&2
  exit 1
fi

echo "Stapling notarization ticket..."
xcrun stapler staple "${APP_BUNDLE}"
xcrun stapler validate "${APP_BUNDLE}"

echo "Assessing Gatekeeper acceptance..."
spctl --assess --type execute --verbose=4 "${APP_BUNDLE}"

echo "Creating final distributable zip..."
ditto -c -k --sequesterRsrc --keepParent "${APP_BUNDLE}" "${OUTPUT_ZIP}"
ls -lh "${OUTPUT_ZIP}"
