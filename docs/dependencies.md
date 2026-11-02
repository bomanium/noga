# Dependencies

Every dependency, what it is for and its licence. The rule (CLAUDE.md): licences that allow closed-source commercial
use; LGPL only as separate, replaceable, dynamically loaded binaries with its licence shipped beside them; no GPL or
AGPL. A PR that adds, removes or relicenses a dependency updates this file. Versions are in Directory.Packages.props
and .config/dotnet-tools.json.

## Shipped with the product

| Package | Licence | How it ships |
| --- | --- | --- |
| FrameReader | MIT | A managed assembly (MediaQc.Ffmpeg: the FFmpeg path; MediaQc.Managed uses its managed meters only) |
| ShotDetector | MIT | A managed assembly (the shot list on both paths: MediaQc.Ffmpeg's Shots, MediaQc.Managed) |
| ShotDetector.FastYuv | LGPL-2.1-or-later (ports FFmpeg's swscale code) | A separate managed assembly, ShotDetector.FastYuv.dll, loaded at run time and replaceable (not single-file, not trimmed); converts the managed path's frames for ShotDetector; its COPYING.LGPL goes to a release's licences/shotdetector-fastyuv/, the source and commit in its README.txt (decisions/2026-11-01-frames-bitrate-shots.md) |
| ManagedCodecs (.Core, .Containers, .Aac, .H264, .Mjpeg) | MIT | Managed assemblies (MediaQc.Managed: MP4 demuxing and AAC decoding, no native code) |
| FFmpeg.AutoGen (FrameReader's dependency; MediaQc.Ffmpeg's packet scan uses it directly, at FrameReader's version) | MIT (8.x; older versions were LGPL-3.0) | A managed assembly: the bindings to FFmpeg's libraries |
| .NET runtime (Microsoft.NETCore.App, self-contained in a release) | MIT, with third-party notices | Its licence and THIRD-PARTY-NOTICES.TXT in a release's licences/dotnet/ |
| ASP.NET Core (Microsoft.AspNetCore.App, self-contained in a release) | MIT, with third-party notices | `mediaqc serve`'s server (decisions/2026-11-01-web-ui.md); its LICENSE.txt and THIRD-PARTY-NOTICES.TXT in a release's licences/aspnetcore/ |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent (with Avalonia.Win32, .X11, .Native, .Skia, .HarfBuzz, .FreeDesktop) | MIT | Managed assemblies (MediaQc.App, the desktop app; decisions/2026-11-01-desktop-app.md) |
| SkiaSharp, HarfBuzzSharp (and their NativeAssets) | MIT; the native libSkiaSharp and libHarfBuzzSharp include Skia (BSD-3-Clause), HarfBuzz (MIT), FreeType (FreeType licence), libpng, libjpeg-turbo, libwebp, expat and others under permissive licences | Native libraries beside the app; their THIRD-PARTY-NOTICES.txt in a release's licences/skiasharp/ (libmicrohttpd and giflib, which the notices also list, aren't compiled in: checked) |
| Avalonia.Angle.Windows.Natives (ANGLE) | BSD-3-Clause | av_libglesv2.dll on Windows; its LICENSE in licences/angle/ |
| MicroCom.Runtime, Tmds.DBus.Protocol | MIT | Managed assemblies (Avalonia's COM interop; D-Bus on Linux) |
| FrameReader.Native.linux-x64, .win-x64 | LGPL-2.1-or-later (FFmpeg 8 without `--enable-gpl`; dav1d BSD-2-Clause, libwebp BSD-3-Clause, OpenSSL Apache-2.0 linked in) | Separate shared libraries under `runtimes/<rid>/native/`, loaded at run time and replaceable; the package's COPYING files, configure line, SBOM and README go to a release's licences/ffmpeg/ (tools/publish.sh) |

## Build and test only (not shipped)

| Package | Licence | What for |
| --- | --- | --- |
| StyleBro.Analyzers | MIT | Style analyzers on build (`PrivateAssets="all"`: not in any output) |
| StyleBro.Tool (`bro`) | MIT | The formatter (`dotnet tool run bro format`), local tool |
| xunit | Apache-2.0 | Tests |
| xunit.v3, Avalonia.Headless.XUnit | Apache-2.0, MIT | The desktop app's tests (MediaQc.AppTests), headless |
| xunit.runner.visualstudio | Apache-2.0 | Test runner |
| Microsoft.NET.Test.Sdk | MIT | Test host |
| GitHubActionsTestLogger | MIT | Failed tests as CI annotations (2.4.1 in MediaQc.AppTests: 3.x needs a newer test platform than xunit.v3 3.2) |

## Tools (not shipped)

| Tool | Licence | What for |
| --- | --- | --- |
| ffmpeg and ffprobe (whatever is on PATH; CI installs Ubuntu's) | LGPL-2.1 or GPL, by build | `tools/make-samples.sh` makes test files with it, and the tests compare the measures (and the timecode tag) with them; nothing from it is shipped, so a GPL build (libx264) is fine there |
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
