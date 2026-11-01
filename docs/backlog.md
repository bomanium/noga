# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

- Packaging: a release of `mediaqc` with FFmpeg's licence files beside its libraries
  - Why: CLAUDE.md's licence rule: FrameReader.Native's LGPL libraries ship separate and replaceable, with the LGPL
    text, the build's configure line and its SBOM beside them (the package has them: COPYING.*, configure.txt,
    sbom.spdx.json); nothing copies them into a build's output yet. Also which platforms (linux-x64 and win-x64 so
    far) and the form (a self-contained folder per platform, a zip)
  - Status: Open

## Waiting

Nothing.

## Maybe

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
