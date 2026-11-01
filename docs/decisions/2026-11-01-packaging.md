# Packaging

With v1's checks done (PR 10), the only Next item was packaging: CLAUDE.md's licence rule ships FFmpeg's LGPL
libraries separate and replaceable, with their licence beside them, and nothing put them there yet.

Question: take packaging now?

Choices:

1. Packaging (recommended): a publish script and a CI job that build self-contained linux-x64 and win-x64 folders and
   archives with the LGPL files beside FFmpeg's libraries, and a check that they're there.
2. Report polish first: which decoder measured the file and its facts, in both reports.
3. Stop here.

Answer: 1. `tools/publish.sh` builds `artifacts/mediaqc-<version>-<rid>/` (self-contained: the .NET runtime and
FFmpeg's libraries with it) and its archive (.tar.gz for Linux, .zip for Windows). `MediaQc.Cli.csproj`'s
PublishLicences target adds, to any publish for a platform: FrameReader.Native's COPYING files, configure.txt,
sbom.spdx.json and README in `licences/ffmpeg/`; the .NET runtime's LICENSE.TXT and THIRD-PARTY-NOTICES.TXT in
`licences/dotnet/`; `packaging/THIRD-PARTY-NOTICES.txt` (the MIT libraries) in `licences/`; and `packaging/README.txt`
(the command, and how to replace FFmpeg's libraries with one's own build of the same version). The script checks each
folder has its libraries and licences; CI runs it and the Linux build on a sample. A release workflow, macOS and
signing are on the backlog as Maybe.
