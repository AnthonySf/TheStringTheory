#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
OUT_DIR="${REPO_ROOT}/Assets/Plugins/macOS"
WORK_DIR="${MACOS_NATIVE_WORK_DIR:-${REPO_ROOT}/Temp/macos-native-plugins}"

MACOS_ARCHS="${MACOS_ARCHS:-x86_64;arm64}"
MACOS_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET:-11.0}"
ONNXRUNTIME_VERSION="${ONNXRUNTIME_VERSION:-1.19.2}"
PORTAUDIO_VERSION="${PORTAUDIO_VERSION:-v19.7.0}"
MACOS_ADHOC_CODESIGN="${MACOS_ADHOC_CODESIGN:-1}"

require_macos() {
  if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script must be run on macOS. The project-native dylibs require Xcode's macOS SDK." >&2
    exit 1
  fi
}

require_tool() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required tool: $1" >&2
    exit 1
  fi
}

make_clean_dir() {
  rm -rf "$1"
  mkdir -p "$1"
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

copy_first_match() {
  local source_dir="$1"
  local pattern="$2"
  local destination="$3"
  local source_file

  source_file="$(find "${source_dir}" -name "${pattern}" \( -type f -o -type l \) | sort | head -n 1)"
  if [[ -z "${source_file}" ]]; then
    echo "Could not find ${pattern} under ${source_dir}" >&2
    exit 1
  fi

  cp -L "${source_file}" "${destination}"
}

fix_dylib_identity() {
  local dylib="$1"
  local name
  name="$(basename "${dylib}")"

  if command -v install_name_tool >/dev/null 2>&1; then
    install_name_tool -id "@loader_path/${name}" "${dylib}" 2>/dev/null || true
    install_name_tool -add_rpath "@loader_path" "${dylib}" 2>/dev/null || true
  fi
}

adhoc_codesign() {
  if [[ "${MACOS_ADHOC_CODESIGN}" != "1" ]]; then
    return
  fi

  if command -v codesign >/dev/null 2>&1; then
    codesign --force --sign - --timestamp=none "$1" >/dev/null || \
      echo "Warning: ad-hoc codesign failed for $1; final app signing should still sign it." >&2
  fi
}

prepare_dylib() {
  local dylib="$1"
  chmod +x "${dylib}"
  fix_dylib_identity "${dylib}"
  adhoc_codesign "${dylib}"
}

select_onnx_package() {
  if [[ "${MACOS_ARCHS}" == *";"* ]]; then
    echo "onnxruntime-osx-universal2-${ONNXRUNTIME_VERSION}.tgz"
  elif [[ "${MACOS_ARCHS}" == "arm64" ]]; then
    echo "onnxruntime-osx-arm64-${ONNXRUNTIME_VERSION}.tgz"
  elif [[ "${MACOS_ARCHS}" == "x86_64" ]]; then
    echo "onnxruntime-osx-x86_64-${ONNXRUNTIME_VERSION}.tgz"
  else
    echo "Unsupported MACOS_ARCHS '${MACOS_ARCHS}'. Use x86_64, arm64, or x86_64;arm64." >&2
    exit 1
  fi
}

install_onnxruntime() {
  local package_name
  local archive
  local extract_dir
  local url

  package_name="$(select_onnx_package)"
  archive="${WORK_DIR}/${package_name}"
  extract_dir="${WORK_DIR}/onnxruntime"
  url="https://github.com/microsoft/onnxruntime/releases/download/v${ONNXRUNTIME_VERSION}/${package_name}"

  download "${url}" "${archive}"
  make_clean_dir "${extract_dir}"
  tar -xzf "${archive}" -C "${extract_dir}" --strip-components 1

  copy_first_match "${extract_dir}/lib" "libonnxruntime.dylib" "${OUT_DIR}/libonnxruntime.dylib"
  prepare_dylib "${OUT_DIR}/libonnxruntime.dylib"

  if find "${extract_dir}/lib" -name "libonnxruntime_providers_shared.dylib" \( -type f -o -type l \) | grep -q .; then
    copy_first_match "${extract_dir}/lib" "libonnxruntime_providers_shared.dylib" "${OUT_DIR}/libonnxruntime_providers_shared.dylib"
    prepare_dylib "${OUT_DIR}/libonnxruntime_providers_shared.dylib"
  fi
}

build_portaudio() {
  local archive
  local source_parent
  local source_dir
  local build_dir
  local version_without_v

  archive="${WORK_DIR}/portaudio-${PORTAUDIO_VERSION}.tar.gz"
  source_parent="${WORK_DIR}/portaudio-src"
  version_without_v="${PORTAUDIO_VERSION#v}"
  source_dir="${source_parent}/portaudio-${version_without_v}"
  build_dir="${WORK_DIR}/portaudio-build"

  download "https://github.com/PortAudio/portaudio/archive/refs/tags/${PORTAUDIO_VERSION}.tar.gz" "${archive}"
  make_clean_dir "${source_parent}"
  tar -xzf "${archive}" -C "${source_parent}"
  make_clean_dir "${build_dir}"

  cmake -S "${source_dir}" -B "${build_dir}" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_OSX_ARCHITECTURES="${MACOS_ARCHS}" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET}" \
    -DPA_BUILD_SHARED=ON \
    -DPA_BUILD_STATIC=OFF \
    -DPA_BUILD_TESTS=OFF \
    -DPA_BUILD_EXAMPLES=OFF \
    -DPA_USE_COREAUDIO=ON \
    -DPA_OUTPUT_OSX_FRAMEWORK=OFF

  cmake --build "${build_dir}" --config Release --target portaudio
  copy_first_match "${build_dir}" "libportaudio*.dylib" "${OUT_DIR}/libportaudio.dylib"
  prepare_dylib "${OUT_DIR}/libportaudio.dylib"
}

build_project_native_plugins() {
  MACOS_ARCHS="${MACOS_ARCHS}" MACOS_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET}" \
    bash "${REPO_ROOT}/NativeNotesDetectorBridge/build-macos.sh"
  prepare_dylib "${OUT_DIR}/libNativeNotesDetectorBridgeNative_v6.dylib"

  MACOS_ARCHS="${MACOS_ARCHS}" MACOS_DEPLOYMENT_TARGET="${MACOS_DEPLOYMENT_TARGET}" \
    bash "${REPO_ROOT}/Native/StringTheoryToneHost/build-macos.sh"
  prepare_dylib "${OUT_DIR}/libStringTheoryToneHost.dylib"
}

verify_output() {
  local missing=0
  local file

  for file in \
    "libNativeNotesDetectorBridgeNative_v6.dylib" \
    "libStringTheoryToneHost.dylib" \
    "libportaudio.dylib" \
    "libonnxruntime.dylib"; do
    if [[ ! -f "${OUT_DIR}/${file}" ]]; then
      echo "Missing ${OUT_DIR}/${file}" >&2
      missing=1
    fi
  done

  if [[ "${missing}" != "0" ]]; then
    exit 1
  fi

  echo
  echo "macOS native plugins are ready in ${OUT_DIR}:"
  ls -lh "${OUT_DIR}"/*.dylib

  if command -v lipo >/dev/null 2>&1; then
    echo
    for file in "${OUT_DIR}"/*.dylib; do
      lipo -info "${file}" || true
    done
  fi
}

require_macos
require_tool cmake
require_tool curl
require_tool tar
require_tool find

mkdir -p "${OUT_DIR}" "${WORK_DIR}"

install_onnxruntime
build_portaudio
build_project_native_plugins
verify_output
