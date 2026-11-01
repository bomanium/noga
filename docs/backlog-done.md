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
