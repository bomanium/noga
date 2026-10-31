# Exit codes, head and tail, timecode

Three details of the v1 profile ([2026-10-31-v1-thresholds.md](2026-10-31-v1-thresholds.md)).

## Exit codes of `mediaqc check`

Choices:

1. 0 pass, 1 failure, 2 usage or read error, 3 warnings; `--warnings-fail` makes warnings 1 (recommended). A plain
   `if mediaqc check ...` catches failures; a pipeline chooses whether warnings stop it.
2. 0 pass or warnings, 1 failure, 2 error: warnings never stop a pipeline.
3. 0 pass, 1 warnings, 2 failure, 3 error: ordered by severity, but `if` treats warnings as failing.

Answer: 1.

## Where a black span or silence counts as head or tail

Choices:

1. Only one that touches the first or the last frame is the programme's lead-in or run-out (reported); any other is
   mid-programme (a warning) (recommended). No arbitrary number.
2. Within 10 s of either end (room for a slate or countdown).
3. Black right after the line-up bars and tone counts as the head too (needs bars detection first).

Answer: 1.

## Timecode in the reports

Choices:

1. SMPTE HH:MM:SS:FF counted from the file's start timecode (its timecode track, e.g. 10:00:00:00), 00:00:00:00
   when it has none; drop-frame (HH:MM:SS;FF) at 29.97 and 59.94; the JSON has seconds too (recommended).
2. Media time HH:MM:SS.mmm from the first frame, ignoring the file's timecode.

Answer: 1.

## Settled with the code (the measurement defaults)

Black: blackdetect's defaults (`pix_th=0.10`, `pic_th=0.98`) with `d=2`. Silence: `silencedetect=n=-60dB:d=2`.
They are the profile's, so a later profile can change them.
