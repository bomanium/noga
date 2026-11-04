#!/usr/bin/env bash
# Runs the linux-arm64 release on an x64 machine, under qemu-user (docs/decisions/2026-11-03-linux-arm64.md): its
# AppImage unpacked and its command's --version, then each sample checked by the arm64 folder and by the x64 one,
# whose findings must be the same. tools/publish.sh linux-x64 linux-arm64 first; needs qemu's aarch64 user-mode
# emulator: qemu-aarch64-static (apt install qemu-user-static, up to Ubuntu 24.04) or qemu-aarch64 (qemu-user, from
# 26.04, where qemu-user-static is only a virtual package). The arm64 libraries the command needs (glibc, libstdc++)
# come from Ubuntu's arm64 base image, pinned by SHA-256 and unpacked into artifacts/arm64-root (a tool here, never shipped), which qemu reads with
# -L, so no binfmt handler is needed. Tiered compilation is off: under qemu-user, .NET's tier-up patching of running
# code crashes the process (SIGSEGV) at random, which it doesn't on ARM.
#
#   tools/arm64-check.sh [sample ...]      default: samples/programme_prores.mov samples/programme_h264.mp4
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
samples=("$@")
[ ${#samples[@]} -gt 0 ] || samples=(samples/programme_prores.mov samples/programme_h264.mp4)
qemu=$(command -v qemu-aarch64-static || command -v qemu-aarch64) \
  || { echo "arm64-check: no qemu-aarch64-static or qemu-aarch64 (apt install qemu-user-static, or qemu-user)" >&2; exit 1; }

root=artifacts/arm64-root
base=artifacts/ubuntu-base-24.04.3-base-arm64.tar.gz
sha=7b2dced6dd56ad5e4a813fa25c8de307b655fdabc6ea9213175a92c48dabb048
if [ ! -e "$root/lib/ld-linux-aarch64.so.1" ]; then
  if ! { [ -f "$base" ] && echo "$sha  $base" | sha256sum -c --quiet - 2> /dev/null; }; then
    curl -sSfL --retry 4 --retry-all-errors -o "$base.part" \
      https://cdimage.ubuntu.com/ubuntu-base/releases/24.04/release/ubuntu-base-24.04.3-base-arm64.tar.gz
    echo "$sha  $base.part" | sha256sum -c --quiet - || { echo "arm64-check: the arm64 base image isn't the pinned one" >&2; rm -f "$base.part"; exit 1; }
    mv "$base.part" "$base"
  fi
  rm -rf "$root" && mkdir -p "$root"
  tar -xzf "$base" -C "$root" --exclude='dev/*' 2> /dev/null || tar -xzf "$base" -C "$root" --exclude='dev/*'
fi

export DOTNET_TieredCompilation=0 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
arm() { "$qemu" -L "$root" "$@"; }

# The AppImage: its runtime (static) unpacks it, and the command inside says its version.
image="$(pwd)/artifacts/noga-$version-aarch64.AppImage"
unpacked=$(mktemp -d)
trap 'rm -rf "$unpacked"' EXIT
(cd "$unpacked" && "$qemu" "$image" --appimage-extract > /dev/null)
said=$(arm "$unpacked/squashfs-root/noga" --version)
[ "$said" = "$version" ] || { echo "arm64-check: the AppImage's noga --version said: $said" >&2; exit 1; }
echo "arm64-check: $image runs ($said)"

# The checks: the same exit code and the same findings (check, severity, value, message) as x64's. Each value to 12
# significant digits: floating-point sums can round their last bit differently on arm64 and x64 (Vector<T> is 128 bits
# wide on one and 256 on the other), as a true peak of make-samples.sh's pink noise, which differs every run, once did
# in its 15th digit; the messages compare the figures as the report shows them, exactly.
out=$(mktemp -d)
trap 'rm -rf "$unpacked" "$out"' EXIT
for sample in "${samples[@]}"; do
  name=$(basename "$sample")
  code_arm=0; arm "artifacts/noga-$version-linux-arm64/noga" check "$sample" --out "$out/arm64" > /dev/null || code_arm=$?
  code_x64=0; "artifacts/noga-$version-linux-x64/noga" check "$sample" --out "$out/x64" > /dev/null || code_x64=$?
  [ "$code_arm" = "$code_x64" ] || { echo "arm64-check: $name exits $code_arm on arm64, $code_x64 on x64" >&2; exit 1; }
  findings() { python3 -c 'import json,sys; v=lambda x: float(f"{x:.12g}") if isinstance(x, float) else x; print(json.dumps([[f["check"], f["severity"], v(f.get("value")), f.get("message")] for f in json.load(open(sys.argv[1]))["findings"]], indent=1))' "$1"; }
  if ! diff <(findings "$out/x64/$name.qc.json") <(findings "$out/arm64/$name.qc.json") > "$out/diff.txt"; then
    echo "arm64-check: $name's findings differ on arm64 (> lines) from x64 (< lines):" >&2
    cat "$out/diff.txt" >&2
    exit 1
  fi
  echo "arm64-check: $name: exit $code_arm and the same findings as x64"
done
