#!/usr/bin/env bash
# Puts noga-app into the desktop's menu (and "Open with" for video files) for this user: writes
# noga-app.desktop into ~/.local/share/applications with this folder's noga-app and noga-app.png as absolute
# paths. Run it again after moving the folder; --uninstall takes the entry out again.
#
#   ./install-desktop-entry.sh [--uninstall]
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
entry="$apps/noga-app.desktop"

if [ "${1:-}" = "--uninstall" ]; then
  rm -f "$entry"
  ! command -v update-desktop-database > /dev/null || update-desktop-database "$apps" 2> /dev/null || true
  echo "noga-app: removed $entry"
  exit 0
fi
[ $# -eq 0 ] || { echo "usage: $0 [--uninstall]" >&2; exit 2; }
for file in noga-app noga-app.png noga-app.desktop; do
  [ -f "$here/$file" ] || { echo "noga-app: no $file in $here" >&2; exit 1; }
done
# The Exec line quotes the path (spaces are fine); these characters would need escaping (twice over, in the desktop
# entry's quoting and in sed's), so refuse them.
case "$here" in
  *[\"\`\$\\%\|\&]* | *$'\n'*) echo "noga-app: move the folder to a path without \" \` \$ \\ % | or &: $here" >&2; exit 1 ;;
esac

mkdir -p "$apps"
sed -e "s|^Exec=.*|Exec=\"$here/noga-app\" %F|" -e "s|^Icon=.*|Icon=$here/noga-app.png|" \
  "$here/noga-app.desktop" > "$entry.tmp"
mv "$entry.tmp" "$entry"
! command -v update-desktop-database > /dev/null || update-desktop-database "$apps" 2> /dev/null || true
echo "noga-app: wrote $entry"
