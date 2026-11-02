# Installers: an NSIS setup.exe and an AppImage

Asked 2026-11-02, after 0.1.0-alpha.2, when the owner chose installers as the next step (backlog, Maybe: the desktop
app's next steps). Both had to be built on CI's ubuntu runner (CLAUDE.md: ubuntu only) with tools whose licences allow
closed-source commercial use.

## Windows: which installer?

- **An NSIS setup.exe** (recommended): made on Linux with makensis (NSIS, zlib licence; its LZMA compressor is CPL-1.0
  with an exception for what it compresses). Per user, no administrator; Start-menu and desktop shortcuts, mediaqc on
  the user's PATH, an uninstaller in Settings, Apps. Unsigned, SmartScreen warns as it does for the zip.
- An MSI with msitools' wixl (LGPL, as a build tool only): deployable by GPO or Intune, but wixl covers part of WiX,
  so per-user installs and PATH are harder.
- Both: two installers to build and test.
- MSIX, waiting for code signing: Windows won't install an unsigned MSIX outside developer mode.

**Answer: an NSIS setup.exe.** (WiX itself runs on Windows only.)

## Linux: which packages?

- **An AppImage** (recommended): one file built with appimagetool (MIT) that runs on most distributions without
  installing; needs FUSE, or `--appimage-extract-and-run`.
- An AppImage and a .deb (dpkg-deb; /opt/mediaqc, a menu entry, mediaqc on PATH).
- A .deb only.

**Answer: an AppImage.**

## The archives

- **Kept** (recommended): the zip and the tar.gz stay in each release, for pipelines and servers that run the command
  from a folder.
- Installers only.

**Answer: kept.**

## The AppImage's runtime and the licence rule

Every AppImage starts with AppImage's type2-runtime (MIT), a separate executable that mounts the files after it; it
links libfuse (LGPL-2.1) statically, with squashfuse, musl, zstd and zlib (permissive). mediaqc doesn't link to it,
and anyone can repack the AppImage with a runtime of their own. Asked whether that is within CLAUDE.md's rule (LGPL
only as separate, replaceable binaries):

- **Accept it, and ship its licences** (recommended): the runtime pinned by SHA-256 to a tagged release (20251108),
  its licences inside the AppImage and beside it on the release, with where its source is; in dependencies.md.
- A .deb instead, for now.
- Neither: the tar.gz only.

**Answer: accept it and ship its licences.**

## What was built

- `packaging/windows/mediaqc.nsi`; `tools/publish.sh` builds `mediaqc-VERSION-win-x64-setup.exe` from the win-x64
  folder and checks, with 7-Zip, that it holds every file of it. The uninstaller deletes the files it installed (a
  list publish.sh writes), not the folder wholesale; a new version runs the old one's uninstaller first. PATH is
  edited in the registry by PowerShell, keeping an expandable PATH's `%VARIABLES%` and any length.
- `packaging/linux/AppRun`: the desktop app, or the `mediaqc` command when the first argument is `check`, `serve`,
  `--help` or `--version`, or when it is run through a link named `mediaqc`. publish.sh builds
  `mediaqc-VERSION-x86_64.AppImage` (appimagetool 1.9.1 and type2-runtime 20251108, both pinned by SHA-256:
  `tools/get-appimage-tools.sh`) and runs its `--version`; CI also checks a sample through it.
- The runtime's licences: `packaging/linux/appimage-runtime-licences.txt`, in the AppImage
  (`licences/appimage-runtime/LICENCES.txt`) and on the release (`mediaqc-VERSION-x86_64.AppImage.licences.txt`).
- Not tried on a Windows machine in CI (ubuntu only): installing, the shortcuts, PATH and uninstalling want one check
  by hand on Windows before a stable release.
