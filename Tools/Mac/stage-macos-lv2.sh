#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
LV2_ROOT="${REPO_ROOT}/Assets/StreamingAssets/ToneLab/LV2"
TARGET_ROOT="${LV2_ROOT}/macos-universal"
WORK_DIR="${MACOS_LV2_WORK_DIR:-${REPO_ROOT}/Temp/macos-lv2-payloads}"
PRUNE_OTHER_PLATFORMS="${MACOS_LV2_PRUNE_OTHER_PLATFORMS:-1}"
MACOS_ARCHS="${MACOS_ARCHS:-x86_64;arm64}"
MACOSX_DEPLOYMENT_TARGET="${MACOSX_DEPLOYMENT_TARGET:-11.0}"

require_macos() {
  if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "This script must run on macOS because it extracts signed DMG/pkg plugin payloads." >&2
    exit 1
  fi
}

download() {
  local url="$1"
  local destination="$2"
  local expected_sha="$3"

  if [[ ! -f "${destination}" ]]; then
    curl -sL --fail --retry 5 --retry-delay 5 --retry-all-errors "${url}" -o "${destination}"
  fi

  local actual_sha
  actual_sha="$(shasum -a 256 "${destination}" | awk '{print $1}')"
  if [[ "${actual_sha}" != "${expected_sha}" ]]; then
    echo "SHA256 mismatch for ${destination}" >&2
    echo "expected: ${expected_sha}" >&2
    echo "actual:   ${actual_sha}" >&2
    exit 1
  fi
}

detach_mount() {
  local mount_point="$1"
  if [[ -d "${mount_point}" ]]; then
    hdiutil detach "${mount_point}" -quiet >/dev/null 2>&1 || true
  fi
}

expand_pkg() {
  local pkg_path="$1"
  local output_dir="$2"

  rm -rf "${output_dir}"
  mkdir -p "$(dirname "${output_dir}")"

  if pkgutil --expand-full "${pkg_path}" "${output_dir}" >/dev/null 2>&1; then
    return
  fi

  rm -rf "${output_dir}"
  pkgutil --expand "${pkg_path}" "${output_dir}"

  while IFS= read -r payload; do
    local payload_dir
    payload_dir="$(dirname "${payload}")/Payload.expanded"
    mkdir -p "${payload_dir}"
    (
      cd "${payload_dir}"
      if file "${payload}" | grep -qi "gzip"; then
        gzip -dc "${payload}" | cpio -idm >/dev/null
      else
        cpio -idm < "${payload}" >/dev/null
      fi
    )
  done < <(find "${output_dir}" -type f -name Payload)
}

mount_and_expand_dmg() {
  local dmg_path="$1"
  local group="$2"
  local mount_point="${WORK_DIR}/mount-${group}"
  local expanded_root="${WORK_DIR}/expanded-${group}"

  rm -rf "${mount_point}" "${expanded_root}"
  mkdir -p "${mount_point}" "${expanded_root}"

  hdiutil attach "${dmg_path}" -mountpoint "${mount_point}" -nobrowse -readonly -quiet

  while IFS= read -r pkg_path; do
    local pkg_name
    pkg_name="$(basename "${pkg_path}" .pkg)"
    expand_pkg "${pkg_path}" "${expanded_root}/${pkg_name}"
  done < <(find "${mount_point}" \( -type d -o -type f \) -name "*.pkg" -prune)

  echo "${mount_point};${expanded_root}"
}

find_bundle() {
  local bundle_name="$1"
  shift

  local root
  for root in "$@"; do
    if [[ -d "${root}" ]]; then
      local match
      match="$(find "${root}" -type d -name "${bundle_name}" -not -path "*/__MACOSX/*" | sort | head -n 1)"
      if [[ -n "${match}" ]]; then
        echo "${match}"
        return
      fi
    fi
  done
}

copy_expected_bundles() {
  local group="$1"
  shift
  local roots_csv="$1"
  shift
  IFS=";" read -r -a roots <<< "${roots_csv}"

  local group_target="${TARGET_ROOT}/${group}"
  mkdir -p "${group_target}"

  local missing=0
  local bundle_name
  for bundle_name in "$@"; do
    local source_bundle
    source_bundle="$(find_bundle "${bundle_name}" "${roots[@]}")"
    if [[ -z "${source_bundle}" ]]; then
      echo "Missing expected macOS LV2 bundle '${bundle_name}' in ${group} package." >&2
      missing=1
      continue
    fi

    rm -rf "${group_target}/${bundle_name}"
    ditto "${source_bundle}" "${group_target}/${bundle_name}"
  done

  if [[ "${missing}" != "0" ]]; then
    exit 1
  fi
}

stage_group() {
  local group="$1"
  local file_name="$2"
  local url="$3"
  local sha="$4"
  shift 4

  local dmg_path="${WORK_DIR}/${file_name}"
  echo "Staging ${group} macOS LV2 bundles..."
  download "${url}" "${dmg_path}" "${sha}"

  local roots_csv
  roots_csv="$(mount_and_expand_dmg "${dmg_path}" "${group}")"

  copy_expected_bundles "${group}" "${roots_csv}" "$@"
  detach_mount "${WORK_DIR}/mount-${group}"
}

ensure_gxplugins_build_dependencies() {
  if ! command -v git >/dev/null 2>&1; then
    echo "git is required to build GxPlugins.lv2 from source." >&2
    exit 1
  fi

  if ! command -v clang++ >/dev/null 2>&1; then
    echo "clang++ is required to build GxPlugins.lv2 from source." >&2
    exit 1
  fi

  if ! command -v pkg-config >/dev/null 2>&1 || ! pkg-config --exists lv2; then
    if ! command -v brew >/dev/null 2>&1; then
      echo "Homebrew is required to install pkg-config/lv2 for the macOS GxPlugins build." >&2
      exit 1
    fi

    brew install pkgconf lv2
  fi
}

patch_gxplugins_source_for_macos() {
  local source_root="$1"

  while IFS= read -r source_file; do
    perl -0pi -e 's/#define __rt_func __attribute__\(\(section\("\.rt\.text"\)\)\)\R#define __rt_data __attribute__\(\(section\("\.rt\.data"\)\)\)/#if defined(__APPLE__)\n#define __rt_func\n#define __rt_data\n#else\n#define __rt_func __attribute__((section(".rt.text")))\n#define __rt_data __attribute__((section(".rt.data")))\n#endif/g' "${source_file}"
  done < <(find "${source_root}" -path "*/plugin/*.cpp" -type f)
}

read_makefile_name() {
  local makefile_path="$1"
  awk -F= '/^[[:space:]]*NAME[[:space:]]*=/{gsub(/[[:space:]]/, "", $2); print $2; exit}' "${makefile_path}"
}

patch_lv2_binary_extensions() {
  local bundle_path="$1"

  while IFS= read -r ttl_file; do
    sed -i '' 's/\.so>/.dylib>/g; s/\.so"/.dylib"/g; s/\.so /.dylib /g' "${ttl_file}"
  done < <(find "${bundle_path}" -type f -name "*.ttl")
}

build_gxplugin_bundle() {
  local source_root="$1"
  local source_dir_name="$2"
  local expected_bundle_name="$3"

  local plugin_root="${source_root}/${source_dir_name}"
  local makefile_path="${plugin_root}/Makefile"
  if [[ ! -f "${makefile_path}" ]]; then
    echo "Missing GxPlugins Makefile: ${source_dir_name}" >&2
    exit 1
  fi

  local plugin_name
  plugin_name="$(read_makefile_name "${makefile_path}")"
  if [[ -z "${plugin_name}" ]]; then
    echo "Could not read NAME from ${makefile_path}" >&2
    exit 1
  fi

  if [[ "${plugin_name}.lv2" != "${expected_bundle_name}" ]]; then
    echo "Unexpected bundle name for ${source_dir_name}: expected ${expected_bundle_name}, got ${plugin_name}.lv2" >&2
    exit 1
  fi

  local plugin_cpp="${plugin_root}/plugin/${plugin_name}.cpp"
  local plugin_ttl_dir="${plugin_root}/plugin"
  local mod_ttl_dir="${plugin_root}/MOD"
  if [[ ! -f "${plugin_cpp}" || ! -d "${plugin_ttl_dir}" ]]; then
    echo "Missing GxPlugins DSP source or LV2 metadata for ${source_dir_name}" >&2
    exit 1
  fi

  local target_bundle="${TARGET_ROOT}/GxPlugins/${expected_bundle_name}"
  rm -rf "${target_bundle}"
  mkdir -p "${target_bundle}"
  if [[ -d "${mod_ttl_dir}" ]]; then
    ditto "${mod_ttl_dir}/" "${target_bundle}/"
  else
    find "${plugin_ttl_dir}" -maxdepth 1 -type f -name "*.ttl" -exec cp {} "${target_bundle}/" \;
  fi
  if [[ ! -f "${target_bundle}/manifest.ttl" ]]; then
    echo "GxPlugins LV2 manifest was not copied for ${source_dir_name}" >&2
    exit 1
  fi

  local arch_args=()
  IFS=";" read -r -a requested_archs <<< "${MACOS_ARCHS}"
  local arch
  for arch in "${requested_archs[@]}"; do
    arch="$(echo "${arch}" | xargs)"
    if [[ -n "${arch}" ]]; then
      arch_args+=("-arch" "${arch}")
    fi
  done

  if [[ "${#arch_args[@]}" == "0" ]]; then
    echo "MACOS_ARCHS did not contain any architectures." >&2
    exit 1
  fi

  local lv2_cflags
  lv2_cflags="$(pkg-config --cflags lv2)"

  (
    cd "${plugin_root}"
    # Build the DSP binary only. Tone Lab does not use LV2 plugin UIs, and the
    # upstream GUI build path depends on Linux/Windows resource/linker tooling.
    clang++ \
      "${arch_args[@]}" \
      -std=c++11 \
      -mmacosx-version-min="${MACOSX_DEPLOYMENT_TARGET}" \
      -D_FORTIFY_SOURCE=2 \
      -I. \
      -I./dsp \
      -I./plugin \
      -fPIC \
      -DPIC \
      -O2 \
      -Wall \
      -ffast-math \
      ${lv2_cflags} \
      "plugin/${plugin_name}.cpp" \
      -dynamiclib \
      -install_name "@rpath/${plugin_name}.dylib" \
      -undefined dynamic_lookup \
      -lm \
      -o "${target_bundle}/${plugin_name}.dylib"
  )

  patch_lv2_binary_extensions "${target_bundle}"
  chmod +x "${target_bundle}/${plugin_name}.dylib"
  xattr -d com.apple.quarantine "${target_bundle}/${plugin_name}.dylib" 2>/dev/null || true
  codesign --force --sign - "${target_bundle}/${plugin_name}.dylib"

  local verify_arch
  for verify_arch in "${requested_archs[@]}"; do
    verify_arch="$(echo "${verify_arch}" | xargs)"
    if [[ -n "${verify_arch}" ]]; then
      lipo -verify_arch "${verify_arch}" "${target_bundle}/${plugin_name}.dylib"
    fi
  done
}

stage_gxplugins_from_source() {
  local source_root="${WORK_DIR}/GxPlugins.lv2"

  echo "Building GxPlugins.lv2 v1.0 macOS bundles from source..."
  ensure_gxplugins_build_dependencies

  rm -rf "${source_root}"
  git clone --depth 1 --branch v1.0 https://github.com/brummer10/GxPlugins.lv2 "${source_root}"
  git -C "${source_root}" submodule update --init --recursive --depth 1 --jobs 8
  patch_gxplugins_source_for_macos "${source_root}"

  mkdir -p "${TARGET_ROOT}/GxPlugins"

  build_gxplugin_bundle "${source_root}" "GxAxisFace.lv2" "gx_AxisFace.lv2"
  build_gxplugin_bundle "${source_root}" "GxBaJaTubeDriver.lv2" "gx_bajatubedriver.lv2"
  build_gxplugin_bundle "${source_root}" "GxBlueAmp.lv2" "gx_blueamp.lv2"
  build_gxplugin_bundle "${source_root}" "GxBoobTube.lv2" "gx_boobtube.lv2"
  build_gxplugin_bundle "${source_root}" "GxBottleRocket.lv2" "gx_bottlerocket.lv2"
  build_gxplugin_bundle "${source_root}" "GxClubDrive.lv2" "gx_clubdrive.lv2"
  build_gxplugin_bundle "${source_root}" "GxCreamMachine.lv2" "gx_CreamMachine.lv2"
  build_gxplugin_bundle "${source_root}" "GxDOP250.lv2" "gx_DOP250.lv2"
  build_gxplugin_bundle "${source_root}" "GxEpic.lv2" "gx_epic.lv2"
  build_gxplugin_bundle "${source_root}" "GxEternity.lv2" "gx_eternity.lv2"
  build_gxplugin_bundle "${source_root}" "GxFz1b.lv2" "gx_maestro_fz1b.lv2"
  build_gxplugin_bundle "${source_root}" "GxFz1s.lv2" "gx_maestro_fz1s.lv2"
  build_gxplugin_bundle "${source_root}" "GxGuvnor.lv2" "gx_guvnor.lv2"
  build_gxplugin_bundle "${source_root}" "GxHeathkit.lv2" "gx_Heathkit.lv2"
  build_gxplugin_bundle "${source_root}" "GxHotBox.lv2" "gx_hotbox.lv2"
  build_gxplugin_bundle "${source_root}" "GxHyperion.lv2" "gx_hyperion.lv2"
  build_gxplugin_bundle "${source_root}" "GxKnightFuzz.lv2" "gx_KnightFuzz.lv2"
  build_gxplugin_bundle "${source_root}" "GxLiquidDrive.lv2" "gx_liquiddrive.lv2"
  build_gxplugin_bundle "${source_root}" "GxLuna.lv2" "gx_luna.lv2"
  build_gxplugin_bundle "${source_root}" "GxMicroAmp.lv2" "gx_MicroAmp.lv2"
  build_gxplugin_bundle "${source_root}" "GxPlexi.lv2" "gx_plexi.lv2"
  build_gxplugin_bundle "${source_root}" "GxQuack.lv2" "gx_quack.lv2"
  build_gxplugin_bundle "${source_root}" "GxSaturator.lv2" "gx_saturate.lv2"
  build_gxplugin_bundle "${source_root}" "GxSD1.lv2" "gx_sd1sim.lv2"
  build_gxplugin_bundle "${source_root}" "GxSD2Lead.lv2" "gx_sd2lead.lv2"
  build_gxplugin_bundle "${source_root}" "GxShakaTube.lv2" "gx_shakatube.lv2"
  build_gxplugin_bundle "${source_root}" "GxSloopyBlue.lv2" "gx_sloopyblue.lv2"
  build_gxplugin_bundle "${source_root}" "GxSlowGear.lv2" "gx_slowgear.lv2"
  build_gxplugin_bundle "${source_root}" "GxSunFace.lv2" "gx_SunFace.lv2"
  build_gxplugin_bundle "${source_root}" "GxSuperFuzz.lv2" "gx_sfp.lv2"
  build_gxplugin_bundle "${source_root}" "GxSupersonic.lv2" "gx_supersonic.lv2"
  build_gxplugin_bundle "${source_root}" "GxSuppaToneBender.lv2" "gx_vstb.lv2"
  build_gxplugin_bundle "${source_root}" "GxSVT.lv2" "gx_ampegsvt.lv2"
  build_gxplugin_bundle "${source_root}" "GxTimRay.lv2" "gx_timray.lv2"
  build_gxplugin_bundle "${source_root}" "GxToneMachine.lv2" "gx_tonemachine.lv2"
  build_gxplugin_bundle "${source_root}" "GxTubeDistortion.lv2" "gx_TubeDistortion.lv2"
  build_gxplugin_bundle "${source_root}" "GxUltraCab.lv2" "gx_ultracab.lv2"
  build_gxplugin_bundle "${source_root}" "GxUVox720k.lv2" "gx_uvox.lv2"
  build_gxplugin_bundle "${source_root}" "GxValveCaster.lv2" "gx_valvecaster.lv2"
  build_gxplugin_bundle "${source_root}" "GxVBassPreAmp.lv2" "gx_voxbass.lv2"
  build_gxplugin_bundle "${source_root}" "GxVintageFuzzMaster.lv2" "gx_vfm.lv2"
  build_gxplugin_bundle "${source_root}" "GxVmk2.lv2" "gx_vmk2d.lv2"
  build_gxplugin_bundle "${source_root}" "GxVoodoFuzz.lv2" "gx_voodoo.lv2"

  local staged_count
  staged_count="$(find "${TARGET_ROOT}/GxPlugins" -mindepth 1 -maxdepth 1 -type d -name "*.lv2" | wc -l | xargs)"
  if [[ "${staged_count}" != "43" ]]; then
    echo "Expected 43 staged GxPlugins bundles, found ${staged_count}." >&2
    exit 1
  fi
}

require_macos
mkdir -p "${WORK_DIR}" "${LV2_ROOT}"
trap 'for mount_point in "${WORK_DIR}"/mount-*; do detach_mount "${mount_point}"; done' EXIT

rm -rf "${TARGET_ROOT}" "${TARGET_ROOT}.meta"
mkdir -p "${TARGET_ROOT}"

if [[ "${PRUNE_OTHER_PLATFORMS}" == "1" ]]; then
  rm -rf "${LV2_ROOT}/win-x64" "${LV2_ROOT}/win-x64.meta"
  rm -rf "${LV2_ROOT}/linux-x64" "${LV2_ROOT}/linux-x64.meta"
fi

stage_group \
  "DPF-Plugins" \
  "DPF-Plugins-v1.7-macos-universal.dmg" \
  "https://github.com/DISTRHO/DPF-Plugins/releases/download/v1.7/DPF-Plugins-v1.7-macos-universal.dmg" \
  "94b996c898a7ba160561113d1c48c4700a6ef65d33f4092ea7289b1af44059e1" \
  "3BandEQ.lv2" \
  "CycleShifter.lv2" \
  "MaBitcrush.lv2" \
  "MaFreeverb.lv2" \
  "MaGigaverb.lv2" \
  "MaPitchshift.lv2" \
  "MVerb.lv2" \
  "PingPongPan.lv2"

stage_group \
  "DragonflyReverb" \
  "dragonfly-reverb-3.2.10-macos-universal.dmg" \
  "https://github.com/michaelwillis/dragonfly-reverb/releases/download/3.2.10/dragonfly-reverb-3.2.10-macos-universal.dmg" \
  "e0b854b92a4e51ce5851320fb1a9f91ab1a4309f997de070e175b3e5cab4adaf" \
  "DragonflyEarlyReflections.lv2" \
  "DragonflyHallReverb.lv2" \
  "DragonflyPlateReverb.lv2" \
  "DragonflyRoomReverb.lv2"

stage_group \
  "ZamPlugins" \
  "zam-plugins-4.5-macos-universal.dmg" \
  "https://github.com/zamaudio/zam-plugins/releases/download/4.5/zam-plugins-4.5-macos-universal.dmg" \
  "ac163eb25781b08936b80c12e809777e3aaa523887d0355504f2e6586c4e43cf" \
  "ZamAutoSat.lv2" \
  "ZaMaximX2.lv2" \
  "ZamComp.lv2" \
  "ZamCompX2.lv2" \
  "ZamDelay.lv2" \
  "ZamDynamicEQ.lv2" \
  "ZamEcho.lv2" \
  "ZamEQ2.lv2" \
  "ZamGate.lv2" \
  "ZamGateX2.lv2" \
  "ZamGEQ31.lv2" \
  "ZamGrains.lv2" \
  "ZamHeadX2.lv2" \
  "ZamNoise.lv2" \
  "ZamPhono.lv2" \
  "ZamTube.lv2" \
  "ZaMultiComp.lv2" \
  "ZaMultiCompX2.lv2" \
  "ZamVerb.lv2"

stage_gxplugins_from_source

find "${TARGET_ROOT}" -type f \( -name "*.dylib" -o -name "*.so" \) -exec chmod +x {} \;
xattr -dr com.apple.quarantine "${TARGET_ROOT}" 2>/dev/null || true

echo
echo "macOS LV2 payload staged in ${TARGET_ROOT}:"
find "${TARGET_ROOT}" -type d -name "*.lv2" | sort
