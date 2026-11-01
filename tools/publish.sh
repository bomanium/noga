#!/usr/bin/env bash
# Builds mediaqc for each platform into artifacts/ (ignored by git): a self-contained folder (the .NET runtime and
# FFmpeg's libraries with it) and its archive, a .tar.gz for Linux and a .zip for Windows, then checks that each holds
# its licences (CLAUDE.md's licence rule: FFmpeg's LGPL libraries ship separate, with their licence, configure line and
# SBOM beside them, ShotDetector.FastYuv's assembly with its licence; MediaQc.Cli.csproj's PublishLicences puts them there).
#
#   tools/publish.sh [rid ...]      default: linux-x64 win-x64
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
rids=("$@")
[ ${#rids[@]} -gt 0 ] || rids=(linux-x64 win-x64)
mkdir -p artifacts

for rid in "${rids[@]}"; do
  name="mediaqc-$version-$rid"
  out="artifacts/$name"
  rm -rf "$out"
  dotnet publish src/MediaQc.Cli -c Release -r "$rid" --self-contained -p:DebugType=none -o "$out" > /dev/null

  case "$rid" in
    win-*) libs=(avcodec-62.dll avformat-62.dll avfilter-11.dll avutil-60.dll swresample-6.dll swscale-9.dll); exe=mediaqc.exe ;;
    *) libs=(libavcodec.so.62 libavformat.so.62 libavfilter.so.11 libavutil.so.60 libswresample.so.6 libswscale.so.9); exe=mediaqc ;;
  esac
  for file in "$exe" "${libs[@]}" README.txt licences/THIRD-PARTY-NOTICES.txt licences/ffmpeg/COPYING.LGPL \
              licences/ffmpeg/configure.txt licences/ffmpeg/sbom.spdx.json licences/dotnet/LICENSE.TXT \
              licences/dotnet/THIRD-PARTY-NOTICES.TXT ShotDetector.FastYuv.dll licences/shotdetector-fastyuv/COPYING.LGPL; do
    [ -f "$out/$file" ] || { echo "publish: $out has no $file" >&2; exit 1; }
  done

  case "$rid" in
    win-*) (cd artifacts && rm -f "$name.zip" && zip -qr "$name.zip" "$name") && echo "publish: artifacts/$name.zip" ;;
    *) tar -czf "artifacts/$name.tar.gz" -C artifacts "$name" && echo "publish: artifacts/$name.tar.gz" ;;
  esac
done
