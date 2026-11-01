# Backlog: done

Finished items from [backlog.md](backlog.md), oldest first.

- Unpark: svarta's feasibility check
  - Why: media-qc was to wait for the owner's masking tool
  - Status: Done (2026-10-31: unparked before it, decisions/2026-10-31-unparked.md)

- Decision: the first formats and the first check profile
  - Status: Done (decisions/2026-10-31-first-formats-and-check-profile.md)

- Decision: where the measures live
  - Status: Done (decisions/2026-10-31-where-the-measures-live.md)

- Decision: decoding
  - Status: Done (decisions/2026-10-31-decoding.md)

- Decision: product shape
  - Status: Done (decisions/2026-10-31-product-shape.md)

- `mediaqc check`: HTML and JSON reports and the exit code
  - Why: docs/decisions/2026-10-31-product-shape.md
  - Status: Done (2026-10-31: `mediaqc check FILE [--out FOLDER] [--warnings-fail]` measures on the FFmpeg path,
    writes FILE.qc.html and FILE.qc.json and exits 0/1/2/3; checks not measured yet (black, bars) are reported as
    such)

- CI generates the samples and compares with FFmpeg
  - Why: Every figure is checked against FFmpeg (CLAUDE.md)
  - Status: Done (2026-11-01: CI runs tools/make-samples.sh; SampleTests measure the programme as ProRes/MOV,
    DNxHR/MXF and H.264/MP4 and check what was put in and each figure against ffmpeg's ebur128, silencedetect,
    bandpass and astats. Black and bars join when they are measured)

- Publish the measures from shotdetector-web's core into FrameReader
  - Why: docs/decisions/2026-10-31-where-the-measures-live.md: one implementation, checked against FFmpeg in the
    public repositories, fed by any frame source. What FrameReader has already (2026-10-31): `Loudness.Measure` (FFmpeg's
    own ebur128 filter over an `AudioReader`, so the FFmpeg path only) and `ReferenceToneFinder` (line-up tone, any
    samples). What only shotdetector-web's core has: PictureMeter/PictureMeasures (black, bars, range, crop, frame
    types, on decoded frames), a managed BS.1770 loudness and true peak, silences and channel peaks (Mp4Audio). Those
    go public, taking frames and samples from either decoder. The work is in the shotdetector and shotdetector-web
    repositories (their own PRs and releases); media-qc then takes the packages
  - Status: Done (2026-11-01: FrameReader 2.19.0 has PictureMeter/PictureMeasures and SilenceFinder beside
    LoudnessMeter and ReferenceToneFinder; media-qc uses them on both paths)

- The v1 profile in MediaQc.Core: verdicts, thresholds, timecodes
  - Why: docs/decisions/2026-10-31-first-formats-and-check-profile.md: EBU R128 (integrated -23 LUFS, true peak
    -1 dBTP, loudness range), black spans, bars, tone and silences, each a pass/warning/failure with timecodes and
    the FFmpeg filter it is checked against. Thresholds: decisions/2026-10-31-v1-thresholds.md; exit codes, head and
    tail, timecode: decisions/2026-10-31-exit-codes-head-tail-timecode.md
  - Status: Done (2026-11-01: every v1 check measured on both paths and judged, with its timecodes and what it is
    checked against)

- Both decoding paths, with a test that they agree
  - Why: docs/decisions/2026-10-31-decoding.md: ManagedCodecs for H.264/AAC MP4, FrameReader (FFmpeg) for the rest;
    the same MP4 through both gives the same measures
  - Status: Done (2026-11-01, decisions/2026-11-01-range-and-crop.md: black, bars, range and crop through PictureMeter
    on both paths; SampleTests check both agree on make-samples.sh's MP4, the picture exactly, and each figure against
    ffmpeg)

- Pixels outside broadcast range and crop/letterbox
  - Why: The next checks after the first profile (decisions/2026-10-31-first-formats-and-check-profile.md); shotdetector-web
    already measures both (signalstats, cropdetect), and make-samples.sh makes files for them
  - Status: Done (2026-11-01, decisions/2026-11-01-range-and-crop.md: a warning over 1% of a frame (EBU R 103), bars
    left out; crop as information)
