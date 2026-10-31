# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to `backlog-done.md` when it is
Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

Nothing while the project is parked (see Waiting).

## Waiting

- Unpark: svarta's feasibility check
  - Why: The owner's masking tool comes first; media-qc waits until svarta passes its feasibility check (2026-10-31)
  - Status: Waiting (on the owner)

- Decision: where the measures live
  - Why: The first profile is EBU R128 loudness, black, bars, tone and silence
    (decisions/2026-10-31-first-formats-and-check-profile.md). shotdetector-web's core (PictureMeter,
    PictureMeasures, Loudness, Mp4Audio) already measures black, bars, range, crop and loudness, checked against FFmpeg. Either they are published from there into the public FrameReader
    or ShotDetector packages, so this repository and the page share one implementation, or they are copied here (MIT
    allows it; keep the notice)
  - Status: Waiting (on the owner)

- Decision: decoding
  - Why: FFmpeg through FrameReader for every format, ManagedCodecs only, or both (managed where possible, FFmpeg for
    the professional formats). Deliveries are often ProRes, DNxHD/DNxHR or MXF, which ManagedCodecs doesn't decode;
    FrameReader's FFmpeg build is LGPL, so it ships as separate, replaceable libraries either way. Since
    decisions/2026-10-31-first-formats-and-check-profile.md, v1 reads ProRes and DNxHR, so FFmpeg is in v1; what is
    left is whether ManagedCodecs also decodes H.264
  - Status: Waiting (on the owner)

- Decision: product shape
  - Why: CLI only, a desktop app, or a local web UI: decides the second project in the solution and how the HTML report
    is shown. The JSON report and the exit code for pipelines come in every shape
  - Status: Waiting (on the owner)

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

- Run make-samples.sh in CI
  - Why: CI only checks that it parses; once tests use the samples, CI has to generate them (an ffmpeg without
    `--enable-gpl` is enough, since the H.264 file is optional)
  - Status: Maybe

- Captions, ad markers and platform spec templates
  - Why: Out of scope at first (docs/notes/origin.md); what customers ask for after the first version decides which
  - Status: Maybe

- Map the checks to EBU's QC test items
  - Why: EBU's catalogue (tech.ebu.ch/qc) names and defines the checks broadcasters know; reports that use its names and
    IDs read as familiar
  - Status: Maybe
