# The v1 profile's thresholds

Question: what the default profile holds a file to (pass, warning, failure).

Choices:

1. EBU R128, strict (recommended): integrated loudness -23 LUFS: pass within ±0.5 LU, warning to ±1 LU, failure
   beyond; true peak at most -1 dBTP (failure above). Black spans and silences of 2 s or more are reported, with a
   warning when one lies mid-programme (not at the head or tail). Bars and tone are reported (where, which level),
   not judged.
2. As 1, with ±1 LU passing (R128's tolerance for live programmes).
3. Report only: no verdicts yet, exit code 0 or 2 (read, or error) until profiles exist.

Answer: 1. The loudness range is reported, not judged (R128 sets no limit for it). "Head" and "tail" (how far from
each end a black span or silence counts as the programme's lead-in or run-out) are settled with the profile's code.
