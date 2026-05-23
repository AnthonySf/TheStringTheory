#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <osx-arm64|osx-x64> <output-directory>" >&2
  exit 2
fi

RID="$1"
OUT_DIR="$2"
PYTHON_VERSION="3.12.9"
PYTHON_MM="3.12"
TORCH_VERSION="2.2.2"

case "$RID" in
  osx-arm64)
    EXPECTED_ARCH="arm64"
    PYTHON_URL="https://github.com/astral-sh/python-build-standalone/releases/download/20250317/cpython-3.12.9+20250317-aarch64-apple-darwin-install_only_stripped.tar.gz"
    PYTHON_SHA256="0a4647b7df3c8eca11071d6cea68a14a4b102bd6fc6afae314e9852510654b7d"
    ;;
  osx-x64)
    EXPECTED_ARCH="x86_64"
    PYTHON_URL="https://github.com/astral-sh/python-build-standalone/releases/download/20250317/cpython-3.12.9+20250317-x86_64-apple-darwin-install_only_stripped.tar.gz"
    PYTHON_SHA256="1a414bf392a7afe08c742502a82edd41893a1144ccbceb184dc5ee6ee9c069c0"
    ;;
  *)
    echo "Unsupported RID: $RID" >&2
    exit 2
    ;;
esac

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must run on macOS." >&2
  exit 1
fi

HOST_ARCH="$(uname -m)"
if [[ "$HOST_ARCH" != "$EXPECTED_ARCH" ]]; then
  echo "Cannot build $RID stem runtime on $HOST_ARCH runner. Expected $EXPECTED_ARCH." >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK_DIR="$ROOT_DIR/Temp/macos-stem-runtime-build/$RID"
RUNTIME_ROOT="$OUT_DIR/$RID"
PYTHON_ROOT="$RUNTIME_ROOT/python"
PYTHON_BIN="$PYTHON_ROOT/bin/python3"

rm -rf "$WORK_DIR" "$RUNTIME_ROOT"
mkdir -p "$WORK_DIR" "$RUNTIME_ROOT"

ARCHIVE="$WORK_DIR/python-standalone.tar.gz"
curl -sL --fail --retry 5 --retry-delay 5 --retry-all-errors "$PYTHON_URL" -o "$ARCHIVE"
ACTUAL_SHA="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
if [[ "$ACTUAL_SHA" != "$PYTHON_SHA256" ]]; then
  echo "python-build-standalone SHA256 mismatch for $RID" >&2
  echo "expected: $PYTHON_SHA256" >&2
  echo "actual:   $ACTUAL_SHA" >&2
  exit 1
fi

tar -xzf "$ARCHIVE" -C "$WORK_DIR"
mv "$WORK_DIR/python" "$PYTHON_ROOT"
chmod +x "$PYTHON_BIN"

"$PYTHON_BIN" -m pip install --no-cache-dir --upgrade \
  "pip==25.0.1" \
  "setuptools==75.8.0" \
  "wheel==0.45.1" \
  "packaging==24.2"

"$PYTHON_BIN" -m pip install --no-cache-dir --prefer-binary \
  "numpy==1.26.4" \
  "torch==$TORCH_VERSION" \
  "torchaudio==$TORCH_VERSION" \
  "soundfile==0.13.1" \
  "einops==0.8.2" \
  "lameenc==1.8.2" \
  "openunmix==1.3.0" \
  "pyyaml==6.0.3" \
  "tqdm==4.67.3" \
  "omegaconf==2.3.0" \
  "antlr4-python3-runtime==4.9.3" \
  "retrying==1.4.2" \
  "submitit==1.5.4" \
  "treetable==0.2.6" \
  "julius==0.2.7" \
  "dora-search==0.1.12" \
  "demucs==4.0.1"

"$PYTHON_BIN" - <<'PY'
import demucs
import numpy
import os
import soundfile
import tempfile
import torch
import torchaudio

with tempfile.TemporaryDirectory() as temp_dir:
    probe_path = os.path.join(temp_dir, "probe.ogg")
    audio = numpy.zeros((4410, 1), dtype="float32")
    soundfile.write(probe_path, audio, 44100, format="OGG", subtype="VORBIS")
    if not os.path.exists(probe_path) or os.path.getsize(probe_path) <= 0:
        raise RuntimeError("soundfile OGG/Vorbis probe produced no data")

print("stem runtime ready")
print("torch", torch.__version__)
print("torchaudio", torchaudio.__version__)
print("numpy", numpy.__version__)
print("soundfile", soundfile.__version__)
print("demucs", getattr(demucs, "__version__", "unknown"))
PY

find "$PYTHON_ROOT" -type d -name "__pycache__" -prune -exec rm -rf {} +
find "$PYTHON_ROOT" -type f -name "*.pyc" -delete
find "$PYTHON_ROOT" -type f -name "*.pyo" -delete

xattr -dr com.apple.quarantine "$PYTHON_ROOT" 2>/dev/null || true

if command -v codesign >/dev/null 2>&1; then
  while IFS= read -r -d '' binary; do
    if file "$binary" | grep -q "Mach-O"; then
      codesign --force --sign - "$binary"
    fi
  done < <(find "$PYTHON_ROOT" -type f \( -perm -111 -o -name "*.dylib" -o -name "*.so" \) -print0)
fi

cat > "$RUNTIME_ROOT/stem-runtime-info.json" <<EOF
{
  "schemaVersion": 1,
  "runtimeId": "$RID",
  "pythonVersion": "$PYTHON_VERSION",
  "pythonSource": "python-build-standalone",
  "torchVersion": "$TORCH_VERSION",
  "demucsVersion": "4.0.1"
}
EOF

echo "Built $RID stem runtime at $RUNTIME_ROOT"
du -sh "$RUNTIME_ROOT"
