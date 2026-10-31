# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

- Publish the measures from shotdetector-web's core into FrameReader/ShotDetector
  - Why: docs/decisions/2026-10-31-where-the-measures-live.md: one implementation, checked against FFmpeg in the
    public repositories, fed by any frame source. What FrameReader has already (2026-10-31): `Loudness.Measure` (FFmpeg's
    own ebur128 filter over an `AudioReader`, so the FFmpeg path only) and `ReferenceToneFinder` (line-up tone, any
    samples). What only shotdetector-web's core has: PictureMeter/PictureMeasures (black, bars, range, crop, frame
    types, on decoded frames), a managed BS.1770 loudness and true peak, silences and channel peaks (Mp4Audio). Those
    go public, taking frames and samples from either decoder. The work is in the shotdetector and shotdetector-web
    repositories (their own PRs and releases); media-qc then takes the packages
  - Status: Open (which package hosts them, and the API, are the owner's to approve)

- The v1 profile in MediaQc.Core: verdicts, thresholds, timecodes
  - Why: docs/decisions/2026-10-31-first-formats-and-check-profile.md: EBU R128 (integrated -23 LUFS, true peak
    -1 dBTP, loudness range), black spans, bars, tone and silences, each a pass/warning/failure with timecodes and
    the FFmpeg filter it is checked against. The thresholds and the timecode form are the owner's to approve
  - Status: Open

- Both decoding paths, with a test that they agree
  - Why: docs/decisions/2026-10-31-decoding.md: ManagedCodecs for H.264/AAC MP4, FrameReader (FFmpeg) for the rest;
    the same MP4 through both gives the same measures
  - Status: Open

- `mediaqc check`: HTML and JSON reports and the exit code
  - Why: docs/decisions/2026-10-31-product-shape.md
  - Status: Open

- CI generates the samples and compares with FFmpeg
  - Why: Every figure is checked against FFmpeg (CLAUDE.md); CI runs tools/make-samples.sh and FFmpeg's ebur128,
    blackdetect and silencedetect on the samples, and the tests compare
  - Status: Open

## Waiting

Nothing.

## Maybe

- The AVC patent pool before selling
  - Why: Software with an H.264 decoder falls under the Via LA AVC pool (the first 100,000 units a year carry no
    royalty, then $0.20 a unit). Check the current terms, and whether decoding through FFmpeg's libraries changes it,
    before the first sale (docs/notes/origin.md)
  - Status: Maybe

- Pixels outside broadcast range and crop/letterbox
  - Why: The next checks after the first profile (decisions/2026-10-31-first-formats-and-check-profile.md); shotdetector-web
    already measures both (signalstats, cropdetect), and make-samples.sh makes files for them
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
