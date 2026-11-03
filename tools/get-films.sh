#!/usr/bin/env bash
# Downloads the open films the weekly film tests check the measures on (tests/Noga.FilmTests), each pinned by
# SHA-256, into a folder that stays out of git (default films/). Blender's open films, CC BY 3.0 (Blender Foundation,
# www.bigbuckbunny.org, mango.blender.org); nothing of them is committed or shipped (CLAUDE.md: never commit media).
# A file already there with the right hash isn't downloaded again. ~650 MB.
#
#   tools/get-films.sh [folder]
set -euo pipefail
out="${1:-films}"
mkdir -p "$out"

# get URL FILE SHA256 [unzip SHA256-of-the-zip]
get() {
  local url=$1 file=$2 sha=$3
  if [ -f "$out/$file" ] && echo "$sha  $out/$file" | sha256sum -c --status; then
    echo "get-films: $file is there"
    return
  fi
  if [ $# -ge 5 ] && [ "$4" = unzip ]; then
    curl -fsSL --retry 3 -o "$out/$file.zip" "$url"
    echo "$5  $out/$file.zip" | sha256sum -c --quiet
    unzip -o -q "$out/$file.zip" "$file" -d "$out"
    rm "$out/$file.zip"
  else
    curl -fsSL --retry 3 -o "$out/$file" "$url"
  fi
  echo "$sha  $out/$file" | sha256sum -c --quiet
  echo "get-films: $file"
}

# Big Buck Bunny, 1080p30 H.264 and MP3 (and AC-3 5.1) in MP4, 10.5 min.
get https://download.blender.org/demo/movies/BBB/bbb_sunflower_1080p_30fps_normal.mp4.zip \
  bbb_sunflower_1080p_30fps_normal.mp4 ae51005850b0ff757fe60c3dd7a12d754d3cd2397d87d939b55235e457f97658 \
  unzip e320fef389ec749117d0c1583945039266a40f25483881c2ff0d33207e62b362

# Tears of Steel, 1280x534 at 24 fps, H.264 and MP3 in MOV, 12 min.
get https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov \
  tears_of_steel_720p.mov efa9062d9cdb7a338e40ad530dfdf234806743f29ae6a1a136b97ece4e588e8f
