#!/usr/bin/env bash
# Builds noga for each platform into artifacts/ (ignored by git): a self-contained folder (the .NET runtime and
# FFmpeg's libraries with it) and its archive, a .tar.gz for Linux and a .zip for Windows, then checks that each holds
# its licences (CLAUDE.md's licence rule: FFmpeg's LGPL libraries ship separate, with their licence, configure line and
# SBOM beside them, ShotDetector.FastYuv's assembly with its licence; Noga.Cli.csproj's PublishLicences puts them there).
# Then the installers (docs/decisions/2026-11-02-installers.md): for win-x64 a setup.exe (NSIS's makensis, from the
# nsis package), for linux-x64 an AppImage (tools/get-appimage-tools.sh fetches appimagetool and its runtime, pinned),
# each from the same folder and checked; --no-installers leaves them out.
#
#   tools/publish.sh [--no-installers] [rid ...]      default: linux-x64 linux-arm64 win-x64
#
# linux-arm64 (decisions/2026-11-03-linux-arm64.md) is built on x64 like the rest; its AppImage's --version runs here
# only on ARM: tools/arm64-check.sh runs it, and checks samples with it, under qemu-user.
set -euo pipefail
cd "$(dirname "$0")/.."
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
installers=1
if [ "${1:-}" = "--no-installers" ]; then installers=0; shift; fi
rids=("$@")
[ ${#rids[@]} -gt 0 ] || rids=(linux-x64 linux-arm64 win-x64)
mkdir -p artifacts

for rid in "${rids[@]}"; do
  name="noga-$version-$rid"
  out="artifacts/$name"
  rm -rf "$out"
  dotnet publish src/Noga.Cli -c Release -r "$rid" --self-contained -p:DebugType=none -o "$out" > /dev/null
  # The desktop app beside it, in the same folder (one .NET runtime, its own executable).
  dotnet publish src/Noga.App -c Release -r "$rid" --self-contained -p:DebugType=none -o "$out" > /dev/null

  case "$rid" in
    win-*) libs=(avcodec-62.dll avformat-62.dll avfilter-11.dll avutil-60.dll swresample-6.dll swscale-9.dll libSkiaSharp.dll licences/angle/LICENSE); exe=noga.exe; app=noga-app.exe ;;
    *) libs=(libavcodec.so.62 libavformat.so.62 libavfilter.so.11 libavutil.so.60 libswresample.so.6 libswscale.so.9 libSkiaSharp.so
             noga-app.png noga-app.desktop install-desktop-entry.sh); exe=noga; app=noga-app ;;
  esac
  for file in "$exe" "$app" "${libs[@]}" licences/skiasharp/THIRD-PARTY-NOTICES.txt README.txt LICENSE.txt licences/THIRD-PARTY-NOTICES.txt licences/ffmpeg/COPYING.LGPL \
              licences/ffmpeg/configure.txt licences/ffmpeg/sbom.spdx.json licences/dotnet/LICENSE.TXT \
              licences/dotnet/THIRD-PARTY-NOTICES.TXT ShotDetector.FastYuv.dll licences/shotdetector-fastyuv/COPYING.LGPL \
              Microsoft.AspNetCore.dll licences/aspnetcore/LICENSE.txt licences/aspnetcore/THIRD-PARTY-NOTICES.TXT \
              profiles/hd-1080i25-prores-hq.json profiles/hd-1080p25-prores-hq.json profiles/web-1080p-h264-aac.json; do
    [ -f "$out/$file" ] || { echo "publish: $out has no $file" >&2; exit 1; }
  done

  # Linux: the desktop entry. install-desktop-entry.sh, run into a scratch data folder, writes a valid entry whose Exec and
  # Icon are this folder's noga-app and its icon (a 256 px PNG).
  case "$rid" in
    linux-*)
      [ -x "$out/install-desktop-entry.sh" ] || { echo "publish: $out/install-desktop-entry.sh isn't executable" >&2; exit 1; }
      file "$out/noga-app.png" | grep -q 'PNG image data, 256 x 256' || { echo "publish: $out/noga-app.png isn't a 256 px PNG" >&2; exit 1; }
      data=$(mktemp -d)
      XDG_DATA_HOME="$data" "$out/install-desktop-entry.sh" > /dev/null
      entry="$data/applications/noga-app.desktop"
      folder=$(cd "$out" && pwd)
      grep -qxF "Exec=\"$folder/noga-app\" %F" "$entry" && grep -qxF "Icon=$folder/noga-app.png" "$entry" \
        || { echo "publish: install-desktop-entry.sh wrote no Exec or Icon for $folder:" >&2; cat "$entry" >&2; exit 1; }
      ! command -v desktop-file-validate > /dev/null || desktop-file-validate "$entry" "$out/noga-app.desktop"
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

[ "$installers" -eq 1 ] || exit 0
for rid in "${rids[@]}"; do
  name="noga-$version-$rid"
  out="artifacts/$name"
  case "$rid" in
    win-x64)
      # The setup.exe: the folder as it is, and the list of its files for the uninstaller (deepest first, then the
      # folders, so it removes what it installed and nothing else a user put there).
      command -v makensis > /dev/null || { echo "publish: no makensis (apt install nsis), or pass --no-installers" >&2; exit 1; }
      files=$(pwd)/artifacts/$name-files.nsh
      (cd "$out" && { find . -type f | sort -r | sed -e 's|^\./||' -e 's|/|\\|g' -e 's|.*|  Delete "$INSTDIR\\&"|'
                      find . -mindepth 1 -type d | sort -r | sed -e 's|^\./||' -e 's|/|\\|g' -e 's|.*|  RMDir "$INSTDIR\\&"|'; }) > "$files"
      setup="artifacts/$name-setup.exe"
      makensis -V2 -DVERSION="$version" -DVERSION4="$(echo "${version%%-*}" | cut -d. -f1-3).0" -DSOURCE="$(pwd)/$out" \
        -DFILES="$files" -DOUTFILE="$(pwd)/$setup" packaging/windows/noga.nsi > /dev/null
      rm "$files"
      # What it holds: every file of the folder (7-Zip reads NSIS installers), when 7z is there; the uninstaller, which
      # some 7-Zip versions list (23.01) and others don't (Ubuntu 26.04's), is left out.
      if command -v 7z > /dev/null; then
        listed=$(7z l -ba -slt "$setup" | sed -n 's/^Path = //p' | tr '\\' '/' | grep -v -e '^\$PLUGINSDIR' -e '^uninstall\.exe$' | sort)
        expected=$(cd "$out" && find . -type f | sed 's|^\./||' | sort)
        [ "$listed" = "$expected" ] || { echo "publish: $setup doesn't hold $out's files:" >&2; diff <(echo "$expected") <(echo "$listed") >&2; exit 1; }
      fi
      echo "publish: $setup"
      ;;
    linux-x64 | linux-arm64)
      # The AppImage: the folder with AppRun (the app, or the command for check, serve, --help and --version), the
      # icon, and the runtime's licences; not install-desktop-entry.sh, as the AppImage's own folder is gone once it ends.
      tools=$(tools/get-appimage-tools.sh)
      appdir="artifacts/$name.AppDir"
      rm -rf "$appdir"
      cp -a "$out" "$appdir"
      rm "$appdir/install-desktop-entry.sh"
      install -m 755 packaging/linux/AppRun "$appdir/AppRun"
      ln -s noga-app.png "$appdir/.DirIcon"
      install -D -m 644 packaging/linux/appimage-runtime-licences.txt "$appdir/licences/appimage-runtime/LICENCES.txt"
      arch=$([ "$rid" = linux-arm64 ] && echo aarch64 || echo x86_64)
      image="artifacts/noga-$version-$arch.AppImage"
      rm -f "$image"
      pack() { ARCH=$arch APPIMAGE_EXTRACT_AND_RUN=1 "$tools/appimagetool-x86_64.AppImage" --no-appstream --runtime-file "$tools/runtime-$arch" "$appdir" "$image"; }
      pack > /dev/null 2>&1 || { echo "publish: appimagetool failed:" >&2; pack >&2; exit 1; }
      rm -rf "$appdir"
      cp packaging/linux/appimage-runtime-licences.txt "$image.licences.txt"
      # It runs: the command through AppRun says its version (unpacked, so FUSE isn't needed), on this machine's
      # architecture (tools/arm64-check.sh runs an aarch64 one on x64).
      if [ "$(uname -m)" = "$arch" ]; then
        said=$(APPIMAGE_EXTRACT_AND_RUN=1 "$image" --version)
        [[ "$said" == *"$version"* ]] || { echo "publish: $image --version said: $said" >&2; exit 1; }
      else
        echo "publish: $image not run here ($(uname -m)): tools/arm64-check.sh runs it under qemu"
      fi
      echo "publish: $image"
      ;;
    *) echo "publish: no installer for $rid" ;;
  esac
done
