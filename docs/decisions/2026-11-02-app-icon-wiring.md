# The desktop app's icon and Linux desktop entry

The backlog's Maybe item "The desktop app's next steps" listed an icon and installers; the task was the icon and a
Linux `.desktop` entry only (no installers, signing or macOS).

Question 1: what the icon should look like (drawn as an SVG in the repository, the PNG sizes and the Windows .ico
generated from it).

Choices:

1. A dark rounded square as a film frame (sprocket holes at the edges), a green waveform across it and a white check
   mark in a corner (recommended).
2. Broadcast colour bars with a check mark in a dark circle over them.
3. A magnifier over a film frame, a check inside the lens.

Answer: none yet: research it in a separate session first, which then presents a couple of ideas. That session
(branch claude/app-icon-concepts) records its own decision (2026-11-02-app-icon.md) and commits the chosen drawing as
`src/MediaQc.App/Assets/icon.svg`.

Question 2: meanwhile, what this session does with the wiring (window icon, Windows executable icon, the generator
script, the Linux desktop entry).

Choices:

1. Wire it with a plain provisional icon.svg, open the PR but don't merge it; once the icon is decided, swap in the
   chosen drawing, regenerate and merge, so no placeholder reaches main (recommended).
2. Wire it and merge with a placeholder; the chosen icon replaces it in its own PR.
3. Wait for the icon and do it all at once.

Answer: 1.

With the code:

- `src/MediaQc.App/Assets/icon.svg`: the chosen drawing (a check cut out of the seven colour bars, PR #25,
  2026-11-02-app-icon.md) replaced the provisional one before merging, and `tools/make-icons.sh` generating `icon-{16,24,32,48,64,128,256}.png` and
  `icon.ico` (16 to 256 px) beside it with rsvg-convert and ImageMagick. The generated files are committed (the output
  is byte-for-byte the same on each run), so a build needs neither tool.
- The window's icon: `MainWindow.AppIcon()`, `icon-256.png` embedded (`MediaQc.App.icon.png`). The Windows
  executable's: `<ApplicationIcon>` (the .ico is read on every platform's build; the Linux publish and the win-x64
  publish from Linux both build, the latter with the icon in mediaqc-app.exe).
- Linux: the release folder holds `mediaqc-app.png`, `mediaqc-app.desktop` (Exec=mediaqc-app %F, Icon=mediaqc-app,
  video MIME types for "Open with", StartupWMClass=mediaqc-app) and `install-desktop-entry.sh`, which writes that entry
  into `~/.local/share/applications` with the folder's absolute paths in Exec and Icon (`--uninstall` takes it out).
  A desktop entry can't name a file relative to itself, hence the script. The X11 window's class is set to
  `mediaqc-app` (Program.BuildAvaloniaApp) to match StartupWMClass; on a virtual display (Xvfb) the published app's
  window had WM_CLASS mediaqc-app and its icon in _NET_WM_ICON.
- Tests: the headless app test that the window has the icon (and that the embedded PNG is 256 px); tools/publish.sh
  checks the three files, runs the script into a scratch folder and checks what it writes (desktop-file-validate too,
  where it is installed).
