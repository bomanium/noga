# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

- The shot list: ShotDetector's shots in the report, on both paths
  - Why: The description promises the shot list; decisions/2026-11-01-frames-bitrate-shots.md: information only,
    PySceneDetect detect-adaptive's through ShotDetector (trusted, checked there), the managed path feeding
    ManagedCodecs' frames through ShotDetector.FastYuv (LGPL, shipped as a separate DLL with its licence)
  - Status: Open

## Waiting

Nothing.

## Maybe

- A release workflow: on a tag, CI runs tools/publish.sh and attaches the archives to a GitHub release
  - Why: today a release is built by hand (tools/publish.sh) and CI only checks it builds and runs
  - Status: Maybe

- macOS (osx-arm64) and Linux arm64 releases
  - Why: FrameReader.Native has osx-arm64 and linux-arm64 packages; CI can't run them (ubuntu only, macOS minutes cost
    10x), and macOS wants a signed, notarised binary
  - Status: Maybe

- Code signing for the Windows build
  - Why: an unsigned mediaqc.exe from a download is blocked or warned about by SmartScreen
  - Status: Maybe

- The AVC patent pool before selling
  - Why: Software with an H.264 decoder falls under the Via LA AVC pool (the first 100,000 units a year carry no
    royalty, then $0.20 a unit). Check the current terms, and whether decoding through FFmpeg's libraries changes it,
    before the first sale (docs/notes/origin.md)
  - Status: Maybe

- Open films in CI
  - Why: Generated files have known answers but not real pictures; Blender's open films, downloaded and pinned by
    SHA-256 (as shotdetector's realworld.py does), would check the measures on real material, nightly
  - Status: Maybe

- Captions, ad markers and platform spec templates
  - Why: Out of scope at first (docs/notes/origin.md); what customers ask for after the first version decides which
  - Status: Maybe

- Map the checks to EBU's QC test items
  - Why: EBU's catalogue (tech.ebu.ch/qc) names and defines the checks broadcasters know; reports that use its names and
    IDs read as familiar
  - Status: Maybe
