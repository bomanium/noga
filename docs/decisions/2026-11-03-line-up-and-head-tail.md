# The line-up tone's level, and the head and tail

Asked 2026-11-03, after freeze frames, when the owner chose bars, tone and the head and tail judged next (the
2026-11-03 review: they were reported, never judged).

## The tone's level

- **EBU -18, ATSC -20, within ±0.5 dB** (recommended): ebu-r128 expects -18 dBFS (EBU R 68) and atsc-a85 -20 dBFS
  (SMPTE RP 155); op-59 and arib-tr-b32 set none until their own documents are read, as their loudness figures were
  (decisions/2026-11-02-op59-arib-figures.md); a profile file sets `toneLevel` and `toneTolerance`. A tone outside it
  warns.
- Only from profile files.
- The same levels within ±1 dB, a failure.

**Answer: EBU -18, ATSC -20, within ±0.5 dB.**

## The head and the tail

- **Bars, tone and black; a miss fails** (recommended): profile file fields `headBars` and `headTone` (seconds at least
  of bars and of tone from the start), `headBlack` and `tailBlack` (seconds at least of black with silence before the
  programme and at the end); a file short of one fails. None in the shipped profiles; the HD templates get typical
  values (30 s of bars and tone, 10 s of black before, 5 s after).
- The same fields, a miss warns.
- The tone's level only, for now.

**Answer: bars, tone and black; a miss fails.**

## What was built

- `Profile.ToneLevel` (null: not judged), `ToneTolerance` (0.5); each track's `tone` finding passes or warns, at the
  report's precision (0.1 dB), against the tone's level as measured already (ReferenceToneFinder: a sine's peak, AES17,
  checked against astats within 0.1 dB).
- `Profile.HeadBars`, `HeadTone`, `HeadBlack`, `TailBlack` (null: not asked), judged from what is measured already:
  - `head.bars`: the bars starting at the first frame, their length.
  - `head.tone`: the first track's tone starting in the first 0.5 s, its length.
  - `head.black`: the black span starting at the end of the head's bars (within a frame), or at the start without
    bars; its length that is silent too (the first track's silences).
  - `tail.black`: the black span ending at the last frame, its length that is silent too.
  - Each a pass at or above what is asked, else a failure, with where it is.
- EBU QC items: bars 0052B, tone 0014B, black 0272B.
- `hd-1080i25-prores-hq` and `hd-1080p25-prores-hq` ask for 30 s of bars and tone, 10 s of black before and 5 s after.
