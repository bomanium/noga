# Dependencies

Every dependency, what it is for and its licence. The rule (CLAUDE.md): Noga is MIT, and its dependencies keep a release usable by anyone, closed-source
commercial use included: LGPL only as separate, replaceable, dynamically loaded binaries with its licence shipped beside them; no GPL or
AGPL. A PR that adds, removes or relicenses a dependency updates this file. Versions are in Directory.Packages.props
and .config/dotnet-tools.json.

## Shipped with the product

| Package | Licence | How it ships |
| --- | --- | --- |
| FrameReader | MIT | A managed assembly (Noga.Ffmpeg: the FFmpeg path; Noga.Managed uses its managed meters only) |
| ShotDetector | MIT | A managed assembly (the shot list on both paths: Noga.Ffmpeg's Shots, Noga.Managed) |
| ShotDetector.FastYuv | LGPL-2.1-or-later (ports FFmpeg's swscale code) | A separate managed assembly, ShotDetector.FastYuv.dll, loaded at run time and replaceable (not single-file, not trimmed); converts the managed path's frames for ShotDetector; its COPYING.LGPL goes to a release's licences/shotdetector-fastyuv/, the source and commit in its README.txt (decisions/2026-11-01-frames-bitrate-shots.md) |
| ManagedCodecs (.Core, .Containers, .Aac, .H264, .Mjpeg) | MIT | Managed assemblies (Noga.Managed: MP4 demuxing and AAC decoding, no native code) |
| FFmpeg.AutoGen (FrameReader's dependency; Noga.Ffmpeg's packet scan uses it directly, at FrameReader's version) | MIT (8.x; older versions were LGPL-3.0) | A managed assembly: the bindings to FFmpeg's libraries |
| .NET runtime (Microsoft.NETCore.App, self-contained in a release) | MIT, with third-party notices | Its licence and THIRD-PARTY-NOTICES.TXT in a release's licences/dotnet/ |
| ASP.NET Core (Microsoft.AspNetCore.App, self-contained in a release) | MIT, with third-party notices | `noga serve`'s server (decisions/2026-11-01-web-ui.md); its LICENSE.txt and THIRD-PARTY-NOTICES.TXT in a release's licences/aspnetcore/ |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent (with Avalonia.Win32, .X11, .Native, .Skia, .HarfBuzz, .FreeDesktop) | MIT | Managed assemblies (Noga.App, the desktop app; decisions/2026-11-01-desktop-app.md) |
| SkiaSharp, HarfBuzzSharp (and their NativeAssets) | MIT; the native libSkiaSharp and libHarfBuzzSharp include Skia (BSD-3-Clause), HarfBuzz (MIT), FreeType (FreeType licence), libpng, libjpeg-turbo, libwebp, expat and others under permissive licences | Native libraries beside the app; their THIRD-PARTY-NOTICES.txt in a release's licences/skiasharp/ (libmicrohttpd and giflib, which the notices also list, aren't compiled in: checked) |
| Avalonia.Angle.Windows.Natives (ANGLE) | BSD-3-Clause | av_libglesv2.dll on Windows; its LICENSE in licences/angle/ |
| MicroCom.Runtime, Tmds.DBus.Protocol | MIT | Managed assemblies (Avalonia's COM interop; D-Bus on Linux) |
| FrameReader.Native.linux-x64, .linux-arm64, .win-x64 | LGPL-2.1-or-later (FFmpeg 8 without `--enable-gpl`; dav1d BSD-2-Clause, libwebp BSD-3-Clause, OpenSSL Apache-2.0 linked in) | Separate shared libraries under `runtimes/<rid>/native/`, loaded at run time and replaceable; the package's COPYING files, configure line, SBOM and README go to a release's licences/ffmpeg/ (tools/publish.sh) |
| NSIS's installer stub (in noga-VERSION-win-x64-setup.exe) | zlib; its LZMA decompressor CPL-1.0 with an exception for linked code (packaging/windows/nsis-COPYING.txt) | The setup.exe's own code, not installed; the zlib licence asks no notice in binaries, the LZMA exception frees what it compresses from the CPL (decisions/2026-11-02-installers.md) |
| AppImage's type2-runtime 20251108 (at the start of noga-VERSION-x86_64.AppImage) | MIT; statically links libfuse (LGPL-2.1), squashfuse (BSD-2-Clause), musl (MIT), zstd (BSD-3-Clause), zlib (zlib) | A separate executable that mounts the AppImage's files; noga doesn't link to it, and it can be replaced by repacking with any type2-runtime (`--appimage-extract`, `appimagetool --runtime-file`). Pinned by SHA-256 (tools/get-appimage-tools.sh); its licences and source in packaging/linux/appimage-runtime-licences.txt, inside the AppImage (licences/appimage-runtime/) and beside it on the release (decisions/2026-11-02-installers.md) |

## Build and test only (not shipped)

| Package | Licence | What for |
| --- | --- | --- |
| StyleBro.Analyzers | MIT | Style analyzers on build (`PrivateAssets="all"`: not in any output) |
| StyleBro.Tool (`bro`) | MIT | The formatter (`dotnet tool run bro format`), local tool |
| xunit | Apache-2.0 | Tests |
| Avalonia.BuildServices (Avalonia's dependency) | MIT | Build time only: its AvaloniaStats target would send build telemetry to avaloniaui.net; Directory.Build.targets replaces it with an empty one, so no build sends anything |
| xunit.v3, Avalonia.Headless.XUnit | Apache-2.0, MIT | The desktop app's tests (Noga.AppTests), headless |
| xunit.runner.visualstudio | Apache-2.0 | Test runner |
| Microsoft.NET.Test.Sdk | MIT | Test host |
| GitHubActionsTestLogger | MIT | Failed tests as CI annotations (2.4.1 in Noga.AppTests: 3.x needs a newer test platform than xunit.v3 3.2) |

## Tools (not shipped)

| Tool | Licence | What for |
| --- | --- | --- |
| ffmpeg and ffprobe (whatever is on PATH; CI installs Ubuntu's) | LGPL-2.1 or GPL, by build | `tools/make-samples.sh` makes test files with it, and the tests compare the measures (and the timecode tag) with them; nothing from it is shipped, so a GPL build (libx264) is fine there |
| rsvg-convert (librsvg, LGPL-2.1-or-later) and ImageMagick (ImageMagick licence, Apache-2.0 style) | as named | `tools/make-icons.sh` renders the app's icon.svg into its PNGs and .ico, which are committed; the tools aren't needed to build and nothing from them is shipped |
| makensis (NSIS 3, CI installs Ubuntu's nsis package) | zlib (the compiler; CPL-1.0 for its LZMA module) | Builds the Windows setup.exe on Linux (tools/publish.sh) |
| appimagetool 1.9.1 (with its mksquashfs, desktop-file-validate and zsyncmake) | MIT (the tool; squashfs-tools GPL-2.0, run as a program, nothing of it shipped) | Builds the AppImage (tools/publish.sh), pinned by SHA-256 (tools/get-appimage-tools.sh) |
| qemu-user-static or qemu-user (QEMU, GPL-2.0; Ubuntu 26.04 has only the second) and Ubuntu 24.04's arm64 base image (Ubuntu's packages, GPL and others) | as named | `tools/arm64-check.sh` runs the linux-arm64 release on x64 CI under emulation, the image (pinned by SHA-256) giving it arm64's glibc and libstdc++; run as programs, nothing from them shipped |
| 7-Zip (p7zip-full) | LGPL-2.1 with unRAR restriction | publish.sh lists the setup.exe's files with it to check them; nothing shipped |
| GitHub Actions: actions/checkout, actions/setup-dotnet | MIT | CI, pinned by commit SHA |

## Candidates (the open decisions, docs/backlog.md)

| Package | Licence | How it would ship |
| --- | --- | --- |
| ShotDetector | MIT | A managed assembly |
| ShotDetector.FastYuv | LGPL-2.1-or-later | Only as its own replaceable assembly, with the LGPL text; or not at all |
| shotdetector-web's core (copied, if decided so) | MIT | Source in this repository, keeping its copyright and licence notice |

Patents are a separate question from licences: an H.264 decoder in a sold product falls under the Via LA AVC pool, and
an AAC decoder under its AAC pool
(docs/notes/origin.md).
