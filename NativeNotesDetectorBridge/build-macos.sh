#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
MACOS_ARCHS="${MACOS_ARCHS:-x86_64;arm64}"
MACOS_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET:-11.0}"
AUBIO_VERSION="${AUBIO_VERSION:-0.4.9}"
LIBSAMPLERATE_REF="${LIBSAMPLERATE_REF:-2ccde9568cca73c7b32c97fefca2e418c16ae5e3}"
BUILD_SUFFIX="${MACOS_ARCHS//;/_}"
BUILD_SUFFIX="${BUILD_SUFFIX// /}"
BUILD_DIR="${SCRIPT_DIR}/build/macos-${BUILD_SUFFIX}"
OUT_DIR="${REPO_ROOT}/Assets/Plugins/macOS"
DEPS_WORK_DIR="${REPO_ROOT}/Temp/native-notes-macos-deps"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS because it needs the Apple SDK/Xcode toolchain." >&2
  exit 1
fi

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required tool: $1" >&2
    exit 1
  fi
}

download() {
  local url="$1"
  local destination="$2"

  if [[ -f "${destination}" ]]; then
    return
  fi

  echo "Downloading ${url}"
  curl --fail --location --retry 3 --output "${destination}" "${url}"
}

extract_dependency() {
  local name="$1"
  local url="$2"
  local destination="$3"
  local archive="${DEPS_WORK_DIR}/${name}.tar.gz"
  local extract_dir="${DEPS_WORK_DIR}/${name}"

  if [[ -d "${destination}" ]]; then
    return
  fi

  mkdir -p "${DEPS_WORK_DIR}" "$(dirname "${destination}")"
  download "${url}" "${archive}"
  rm -rf "${extract_dir}" "${destination}"
  mkdir -p "${extract_dir}"
  tar -xzf "${archive}" -C "${extract_dir}" --strip-components 1
  mv "${extract_dir}" "${destination}"
}

ensure_detector_sources() {
  extract_dependency \
    "aubio-${AUBIO_VERSION}" \
    "https://github.com/aubio/aubio/archive/refs/tags/${AUBIO_VERSION}.tar.gz" \
    "${REPO_ROOT}/External/aubio"

  if [[ ! -f "${REPO_ROOT}/External/aubio/src/config.h" ]]; then
    cat > "${REPO_ROOT}/External/aubio/src/config.h" <<'AUBIO_CONFIG'
#ifndef AUBIO_CONFIG_H
#define AUBIO_CONFIG_H

#define HAVE_STDLIB_H 1
#define HAVE_STDIO_H 1
#define HAVE_MATH_H 1
#define HAVE_STRING_H 1
#define HAVE_ERRNO_H 1
#define HAVE_LIMITS_H 1
#define HAVE_STDARG_H 1
#define HAVE_COMPLEX_H 1
#define HAVE_C99_VARARGS_MACROS 1

#endif
AUBIO_CONFIG
  fi

  extract_dependency \
    "libsamplerate-${LIBSAMPLERATE_REF}" \
    "https://github.com/libsndfile/libsamplerate/archive/${LIBSAMPLERATE_REF}.tar.gz" \
    "${REPO_ROOT}/External/libsamplerate"
}

require_tool cmake
require_tool curl
require_tool tar
ensure_detector_sources

cmake -S "${SCRIPT_DIR}" -B "${BUILD_DIR}" \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES="${MACOS_ARCHS}" \
  -DCMAKE_OSX_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET}"

cmake --build "${BUILD_DIR}" --config Release
mkdir -p "${OUT_DIR}"
DETECTOR_DYLIB="$(find "${BUILD_DIR}" -name 'libNativeNotesDetectorBridgeNative_v6.dylib' -type f | head -n 1)"
if [[ -z "${DETECTOR_DYLIB}" ]]; then
  echo "Built detector dylib was not found under ${BUILD_DIR}." >&2
  exit 1
fi
cp "${DETECTOR_DYLIB}" "${OUT_DIR}/"
