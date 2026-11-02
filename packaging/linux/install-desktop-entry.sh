#!/usr/bin/env bash
# Puts mediaqc-app into the desktop's menu (and "Open with" for video files) for this user: writes
# mediaqc-app.desktop into ~/.local/share/applications with this folder's mediaqc-app and mediaqc-app.png as absolute
# paths. Run it again after moving the folder; --uninstall takes the entry out again.
#
#   ./install-desktop-entry.sh [--uninstall]
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
entry="$apps/mediaqc-app.desktop"

if [ "${1:-}" = "--uninstall" ]; then
  rm -f "$entry"
  ! command -v update-desktop-database > /dev/null || update-desktop-database "$apps" 2> /dev/null || true
  echo "mediaqc-app: removed $entry"
  exit 0
fi
[ $# -eq 0 ] || { echo "usage: $0 [--uninstall]" >&2; exit 2; }
for file in mediaqc-app mediaqc-app.png mediaqc-app.desktop; do
  [ -f "$here/$file" ] || { echo "mediaqc-app: no $file in $here" >&2; exit 1; }
done
# The Exec line quotes the path (spaces are fine); these characters would need escaping (twice over, in the desktop
# entry's quoting and in sed's), so refuse them.
case "$here" in
  *[\"\`\$\\%\|\&]* | *$'\n'*) echo "mediaqc-app: move the folder to a path without \" \` \$ \\ % | or &: $here" >&2; exit 1 ;;
esac

mkdir -p "$apps"
sed -e "s|^Exec=.*|Exec=\"$here/mediaqc-app\" %F|" -e "s|^Icon=.*|Icon=$here/mediaqc-app.png|" \
  "$here/mediaqc-app.desktop" > "$entry.tmp"
mv "$entry.tmp" "$entry"
! command -v update-desktop-database > /dev/null || update-desktop-database "$apps" 2> /dev/null || true
echo "mediaqc-app: wrote $entry"
