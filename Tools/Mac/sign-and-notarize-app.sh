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

KEYCHAIN_ARGS=()
if [[ -n "${KEYCHAIN}" ]]; then
  KEYCHAIN_ARGS=(--keychain "${KEYCHAIN}")
fi

codesign_item() {
  local item="$1"
  echo "Signing ${item#${APP_BUNDLE}/}"
  codesign \
    --force \
    --timestamp \
    --options runtime \
    --entitlements "${ENTITLEMENTS}" \
    --sign "${IDENTITY}" \
    "${KEYCHAIN_ARGS[@]}" \
    "${item}"
}

is_macho_file() {
  local file_path="$1"
  file "${file_path}" | grep -Eq 'Mach-O|ar archive'
}

echo "Preparing ${APP_BUNDLE} for Developer ID signing..."
xattr -dr com.apple.quarantine "${APP_BUNDLE}" 2>/dev/null || true

while IFS= read -r binary; do
  codesign_item "${binary}"
done < <(find "${APP_BUNDLE}" -type f \( -name "*.dylib" -o -name "*.so" \) -print | sort)

while IFS= read -r executable; do
  if is_macho_file "${executable}"; then
    codesign_item "${executable}"
  fi
done < <(find "${APP_BUNDLE}/Contents" -type f -perm -111 -print | sort)

while IFS= read -r framework; do
  codesign_item "${framework}"
done < <(find "${APP_BUNDLE}" -type d -name "*.framework" -print | sort)

while IFS= read -r bundle; do
  codesign_item "${bundle}"
done < <(find "${APP_BUNDLE}" -type d \( -name "*.bundle" -o -name "*.plugin" \) -print | sort)

echo "Signing outer app bundle..."
codesign \
  --force \
  --timestamp \
  --options runtime \
  --entitlements "${ENTITLEMENTS}" \
  --sign "${IDENTITY}" \
  "${KEYCHAIN_ARGS[@]}" \
  "${APP_BUNDLE}"

echo "Verifying Developer ID signature..."
codesign --verify --deep --strict --verbose=2 "${APP_BUNDLE}"

SUBMISSION_ZIP="${RUNNER_TEMP:-/tmp}/StringTheory-notary-submit.zip"
rm -f "${SUBMISSION_ZIP}" "${OUTPUT_ZIP}"
ditto -c -k --sequesterRsrc --keepParent "${APP_BUNDLE}" "${SUBMISSION_ZIP}"

echo "Submitting to Apple notarization..."
xcrun notarytool submit "${SUBMISSION_ZIP}" \
  --apple-id "${APPLE_ID}" \
  --team-id "${APPLE_TEAM_ID}" \
  --password "${APPLE_APP_SPECIFIC_PASSWORD}" \
  --wait

echo "Stapling notarization ticket..."
xcrun stapler staple "${APP_BUNDLE}"
xcrun stapler validate "${APP_BUNDLE}"

echo "Assessing Gatekeeper acceptance..."
spctl --assess --type execute --verbose=4 "${APP_BUNDLE}"

echo "Creating final distributable zip..."
ditto -c -k --sequesterRsrc --keepParent "${APP_BUNDLE}" "${OUTPUT_ZIP}"
ls -lh "${OUTPUT_ZIP}"
