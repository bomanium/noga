# The desktop app

Question 1: what next after the profiles.

Choices:

1. Cut a new release, 0.1.0-alpha.2 (recommended).
2. HTTPS for a shared server.
3. A native app shell.
4. Stop here for now.

Answer: 3.

Question 2: which toolkit. WebView2 (Windows) gives the app a dropped file's path (CoreWebView2File.Path); Photino
(Windows, Linux, macOS; Apache-2.0) has no documented way to.

Choices:

1. WebView2, Windows only (recommended).
2. Photino, cross-platform.
3. Not now.

Answer: 2, then, asked which Photino package (PhotinoX, the maintained fork, or upstream Photino.NET), the owner asked
about Avalonia. Avalonia (MIT) draws its own native UI: drag and drop gives local paths on Windows, macOS and Linux
(IStorageItem.TryGetLocalPath), Linux needs no system WebKitGTK, and the UI is testable headless in CI; but it is a
second UI beside the web page (its web view is commercial), and the release grows by Skia.

Question 3: which, given that.

Choices:

1. An Avalonia native app (recommended).
2. A PhotinoX shell around the page.
3. Not now.

Answer: 1.

With the code: `src/MediaQc.App` (`mediaqc-app`, a windowed executable in the same release folder as `mediaqc`):
one window with the profile, a drop zone for files and folders (their media files, subfolders too) and a file picker,
and the checks newest first with their verdicts and buttons opening each report. The checks are the command line's:
`Checker` and `Jobs` moved to `src/MediaQc.Checking`, shared by the CLI, its web UI and the app (with `MediaFiles`,
what counts as media). Built in code, not XAML; Avalonia 12.1.4 with its Fluent theme. Tests: `tests/MediaQc.AppTests`
on Avalonia's headless platform (xunit v3, which Avalonia.Headless.XUnit needs). The published app was also run on a
virtual X11 display (Xvfb) with real files. Licences: Avalonia, SkiaSharp, HarfBuzzSharp, MicroCom and
Tmds.DBus.Protocol are MIT; SkiaSharp's notices (Skia, HarfBuzz, FreeType, ANGLE, libpng and the rest) ship in
licences/skiasharp/, ANGLE's licence in licences/angle/. libmicrohttpd (LGPL) and giflib, which those notices also
list, aren't in the shipped binaries (checked); FreeType is under its FreeType licence, fontconfig is the system's.
