# Dependencies

Every dependency, what it is for and its licence. The rule (CLAUDE.md): licences that allow closed-source commercial
use; LGPL only as separate, replaceable, dynamically loaded binaries with its licence shipped beside them; no GPL or
AGPL. A PR that adds, removes or relicenses a dependency updates this file. Versions are in Directory.Packages.props
and .config/dotnet-tools.json.

## Shipped with the product

| Package | Licence | How it ships |
| --- | --- | --- |
| FrameReader | MIT | A managed assembly (MediaQc.Ffmpeg: the FFmpeg path; MediaQc.Managed uses its managed meters only) |
| ManagedCodecs (.Core, .Containers, .Aac, .H264, .Mjpeg) | MIT | Managed assemblies (MediaQc.Managed: MP4 demuxing and AAC decoding, no native code) |
| FFmpeg.AutoGen (FrameReader's dependency) | MIT (8.x; older versions were LGPL-3.0) | A managed assembly: the bindings to FFmpeg's libraries |
| FrameReader.Native.linux-x64, .win-x64 | LGPL-2.1-or-later (FFmpeg 8 without `--enable-gpl`; dav1d BSD-2-Clause, libwebp BSD-3-Clause, OpenSSL Apache-2.0 linked in) | Separate shared libraries under `runtimes/<rid>/native/`, loaded at run time and replaceable; the package's COPYING files, configure line and SBOM go beside them when the product is packaged |

## Build and test only (not shipped)

| Package | Licence | What for |
| --- | --- | --- |
| StyleBro.Analyzers | MIT | Style analyzers on build (`PrivateAssets="all"`: not in any output) |
| StyleBro.Tool (`bro`) | MIT | The formatter (`dotnet tool run bro format`), local tool |
| xunit | Apache-2.0 | Tests |
| xunit.runner.visualstudio | Apache-2.0 | Test runner |
| Microsoft.NET.Test.Sdk | MIT | Test host |
| GitHubActionsTestLogger | MIT | Failed tests as CI annotations |

## Tools (not shipped)

| Tool | Licence | What for |
| --- | --- | --- |
| ffmpeg (whatever is on PATH; CI installs Ubuntu's) | LGPL-2.1 or GPL, by build | `tools/make-samples.sh` makes test files with it, and the tests compare the measures with it; nothing from it is shipped, so a GPL build (libx264) is fine there |
| ffprobe (whatever is on PATH) | LGPL-2.1 or GPL, by build | `mediaqc check` runs it, a separate process, for the start timecode (decisions/2026-11-01-timecode-via-ffprobe.md); the user's own, not shipped, and the check works without it |
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
