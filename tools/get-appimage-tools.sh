#!/usr/bin/env bash
# Downloads what tools/publish.sh builds the Linux AppImage with, each pinned to a tagged release by SHA-256
# (docs/decisions/2026-11-02-installers.md): appimagetool (MIT; a build tool, not shipped) and AppImage's type2-runtime
# (MIT; the small executable at the start of every AppImage, which mounts the rest; it links libfuse, LGPL-2.1, and is
# shipped with its licences: packaging/linux/appimage-runtime-licences.txt). A file already there with the right hash
# isn't fetched again.
#
#   tools/get-appimage-tools.sh [folder]      default: artifacts/appimage-tools; prints the folder
set -euo pipefail
cd "$(dirname "$0")/.."
folder="${1:-artifacts/appimage-tools}"
mkdir -p "$folder"

fetch() { # name url sha256
  local file="$folder/$1"
  if [ -f "$file" ] && echo "$3  $file" | sha256sum -c --quiet - 2> /dev/null; then return; fi
  curl -sSfL --retry 4 --retry-all-errors -o "$file.part" "$2"
  echo "$3  $file.part" | sha256sum -c --quiet - || { echo "get-appimage-tools: $2 isn't the pinned file" >&2; rm -f "$file.part"; exit 1; }
  mv "$file.part" "$file"
  chmod +x "$file"
}

fetch appimagetool-x86_64.AppImage \
  https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage \
  ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
fetch runtime-x86_64 \
  https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64 \
  2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
echo "$folder"
