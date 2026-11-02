#!/usr/bin/env bash
# Generates the desktop app's icons from src/MediaQc.App/Assets/icon.svg into the same folder: icon-SIZE.png for 16,
# 24, 32, 48, 64, 128 and 256 pixels (the window and the Linux desktop entry use icon-256.png) and icon.ico (all of
# them but 128, for the Windows executable). The generated files are committed, so building needs neither tool; run
# this after changing icon.svg. Needs rsvg-convert (librsvg2-bin) and ImageMagick (convert or magick).
#
#   tools/make-icons.sh
set -euo pipefail
cd "$(dirname "$0")/../src/MediaQc.App/Assets"
command -v rsvg-convert > /dev/null || { echo "make-icons: needs rsvg-convert (librsvg2-bin)" >&2; exit 1; }
if command -v magick > /dev/null; then im=magick; elif command -v convert > /dev/null; then im=convert; else
  echo "make-icons: needs ImageMagick (convert or magick)" >&2; exit 1
fi

for size in 16 24 32 48 64 128 256; do
  rsvg-convert -w "$size" -h "$size" icon.svg -o "icon-$size.png"
done
# PNG-compressed entries, which Windows reads since Vista; -strip leaves out the PNGs' timestamps.
"$im" icon-16.png icon-24.png icon-32.png icon-48.png icon-64.png icon-256.png -strip icon.ico
echo "make-icons: $(pwd)/icon.ico and icon-{16,24,32,48,64,128,256}.png"
