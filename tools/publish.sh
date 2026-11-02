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
  # The desktop app beside it, in the same folder (one .NET runtime, its own executable).
  dotnet publish src/MediaQc.App -c Release -r "$rid" --self-contained -p:DebugType=none -o "$out" > /dev/null

  case "$rid" in
    win-*) libs=(avcodec-62.dll avformat-62.dll avfilter-11.dll avutil-60.dll swresample-6.dll swscale-9.dll libSkiaSharp.dll licences/angle/LICENSE); exe=mediaqc.exe; app=mediaqc-app.exe ;;
    *) libs=(libavcodec.so.62 libavformat.so.62 libavfilter.so.11 libavutil.so.60 libswresample.so.6 libswscale.so.9 libSkiaSharp.so
             mediaqc-app.png mediaqc-app.desktop install-desktop-entry.sh); exe=mediaqc; app=mediaqc-app ;;
  esac
  for file in "$exe" "$app" "${libs[@]}" licences/skiasharp/THIRD-PARTY-NOTICES.txt README.txt licences/THIRD-PARTY-NOTICES.txt licences/ffmpeg/COPYING.LGPL \
              licences/ffmpeg/configure.txt licences/ffmpeg/sbom.spdx.json licences/dotnet/LICENSE.TXT \
              licences/dotnet/THIRD-PARTY-NOTICES.TXT ShotDetector.FastYuv.dll licences/shotdetector-fastyuv/COPYING.LGPL \
              Microsoft.AspNetCore.dll licences/aspnetcore/LICENSE.txt licences/aspnetcore/THIRD-PARTY-NOTICES.TXT; do
    [ -f "$out/$file" ] || { echo "publish: $out has no $file" >&2; exit 1; }
  done

  # Linux: the desktop entry. install-desktop-entry.sh, run into a scratch data folder, writes a valid entry whose Exec and
  # Icon are this folder's mediaqc-app and its icon (a 256 px PNG).
  case "$rid" in
    linux-*)
      [ -x "$out/install-desktop-entry.sh" ] || { echo "publish: $out/install-desktop-entry.sh isn't executable" >&2; exit 1; }
      file "$out/mediaqc-app.png" | grep -q 'PNG image data, 256 x 256' || { echo "publish: $out/mediaqc-app.png isn't a 256 px PNG" >&2; exit 1; }
      data=$(mktemp -d)
      XDG_DATA_HOME="$data" "$out/install-desktop-entry.sh" > /dev/null
      entry="$data/applications/mediaqc-app.desktop"
      folder=$(cd "$out" && pwd)
      grep -qxF "Exec=\"$folder/mediaqc-app\" %F" "$entry" && grep -qxF "Icon=$folder/mediaqc-app.png" "$entry" \
        || { echo "publish: install-desktop-entry.sh wrote no Exec or Icon for $folder:" >&2; cat "$entry" >&2; exit 1; }
      ! command -v desktop-file-validate > /dev/null || desktop-file-validate "$entry" "$out/mediaqc-app.desktop"
      XDG_DATA_HOME="$data" "$out/install-desktop-entry.sh" --uninstall > /dev/null
      [ ! -e "$entry" ] || { echo "publish: install-desktop-entry.sh --uninstall left $entry" >&2; exit 1; }
      rm -rf "$data"
      ;;
  esac

  case "$rid" in
    win-*) (cd artifacts && rm -f "$name.zip" && zip -qr "$name.zip" "$name") && echo "publish: artifacts/$name.zip" ;;
    *) tar -czf "artifacts/$name.tar.gz" -C artifacts "$name" && echo "publish: artifacts/$name.tar.gz" ;;
  esac
done
