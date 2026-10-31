# Dependencies

Every dependency, what it is for and its licence. The rule (CLAUDE.md): licences that allow closed-source commercial
use; LGPL only as separate, replaceable, dynamically loaded binaries with its licence shipped beside them; no GPL or
AGPL. A PR that adds, removes or relicenses a dependency updates this file. Versions are in Directory.Packages.props
and .config/dotnet-tools.json.

## Shipped with the product

None yet.

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
| ffmpeg (whatever is on PATH) | LGPL-2.1 or GPL, by build | `tools/make-samples.sh` makes test files with it; nothing from it is shipped, so a GPL build (libx264) is fine there |
| GitHub Actions: actions/checkout, actions/setup-dotnet | MIT | CI, pinned by commit SHA |

## Candidates (the open decisions, docs/backlog.md)

| Package | Licence | How it would ship |
| --- | --- | --- |
| ManagedCodecs | MIT | A managed assembly |
| FrameReader | MIT | A managed assembly |
| FrameReader.Native.\<rid\> | LGPL-2.1-or-later (our FFmpeg build, without `--enable-gpl`) | Separate shared libraries, loaded at run time and replaceable, with the LGPL text, the build's configure line and its SBOM (what it bundles) |
| ShotDetector | MIT | A managed assembly |
| ShotDetector.FastYuv | LGPL-2.1-or-later | Only as its own replaceable assembly, with the LGPL text; or not at all |
| shotdetector-web's core (copied, if decided so) | MIT | Source in this repository, keeping its copyright and licence notice |

Patents are a separate question from licences: an H.264 decoder in a sold product falls under the Via LA AVC pool
(docs/notes/origin.md).
