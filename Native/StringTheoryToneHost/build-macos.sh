#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
MACOS_ARCHS="${MACOS_ARCHS:-x86_64;arm64}"
MACOS_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET:-11.0}"
BUILD_SUFFIX="${MACOS_ARCHS//;/_}"
BUILD_SUFFIX="${BUILD_SUFFIX// /}"
BUILD_DIR="${SCRIPT_DIR}/build/macos-${BUILD_SUFFIX}"
OUT_DIR="${REPO_ROOT}/Assets/Plugins/macOS"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS because it needs the Apple SDK/Xcode toolchain." >&2
  exit 1
fi

cmake -S "${SCRIPT_DIR}" -B "${BUILD_DIR}" \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES="${MACOS_ARCHS}" \
  -DCMAKE_OSX_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET}"

cmake --build "${BUILD_DIR}" --config Release
mkdir -p "${OUT_DIR}"
TONE_HOST_DYLIB="$(find "${BUILD_DIR}" -name 'libStringTheoryToneHost.dylib' -type f | head -n 1)"
if [[ -z "${TONE_HOST_DYLIB}" ]]; then
  echo "Built Tone Host dylib was not found under ${BUILD_DIR}." >&2
  exit 1
fi
cp "${TONE_HOST_DYLIB}" "${OUT_DIR}/"
