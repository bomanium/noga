# OP-59's and ARIB TR-B32's figures, from the originals

[2026-11-01-profiles.md](2026-11-01-profiles.md) took op-59's and arib-tr-b32's figures from vendor summaries (NUGEN,
RTW, iZotope) and assumed ARIB's tolerance. Checked on 2026-11-02 against the documents themselves; the figures
mediaqc ships stand, and ARIB's ±1 is now the document's, not an assumption.

## The sources

Free TV Australia, Operational Practice OP-59, "Measurement and Management of Loudness in Soundtracks for Television
Broadcasting", Issue 4, October 2018 (the only issue on Free TV's operational practices page,
https://www.freetv.com.au/wp-content/uploads/2019/08/OP-59-Measurement-and-Managemnt-of-Loudness-for-TV-Broadcasting-Issue-4-October-2018.pdf):

- Target: -24 LKFS (3 and 4(b)), measured with an ITU-R BS.1770-3 meter (4(a)).
- Tolerance: "The permitted deviation from the target loudness level of -24LKFS shall generally not exceed +/- 1
  loudness unit, except for programmes where exact normalisation to target level is not practicable" (4(c)).
- True peak: "does not exceed -2dBTP" (5(h)).
- Gate: BS.1770's, relative threshold 10 LU below the ungated loudness (5(d)); the same as mediaqc's ebur128 measure.
- What is measured: short form (commercials, promotions) is the full mix over its whole duration (5(b)); long form
  may be dialogue only or the full mix, as suits the genre (5, 5(a), 5(c)), and says which (5(g)). mediaqc measures
  the full mix, which OP-59 allows for both.
- OP-48 (Audio Levels and Loudness, Issue 4, July 2016), for commercials, refers to OP-59 and gives the same -24 LKFS
  (6).

ARIB TR-B32, "Operational Guidelines for Loudness of Digital Television Programs", version 1.5-E1 (English
translation), 2016-09-29 (https://www.arib.or.jp/english/html/overview/doc/8-TR-B32v1_5-E1.pdf):

- Target: -24 LKFS (2.1.4), the average over the whole programme, start to end; a commercial is a programme of its
  own (2.1.2; Description 1.3).
- Tolerance: "±1 dB of the target loudness level" (2.1.5; 1 dB on the LKFS scale is 1 LU), and Description 1.6: the
  tolerance is for operation, the target stays -24.
- Exception: "For programs where 'creative production requirements' are overriding, the program can be produced with
  a target value below the target loudness level" (2.1.6).
- True peak: at most -1 dBTP (2.2.4), measured on every channel (2.2.3); -3 dBFS when only a sample-peak meter is
  used (2.2.5), which doesn't apply to mediaqc (it measures true peak).
- Gate: BS.1770's, absolute -70 LKFS and relative -10 LU (3.5); channels without LFE (2.1.3).
- What is measured: the full programme mix, not dialogue (Description 1.4). Short-term measurement is outside its
  scope (amendment 1.2; Appendix 2 is informative).

ARIB's current version is 1.7 (2026-09-10), in Japanese, sold to non-members; 1.6 (2025-03-25) is also Japanese only.
Their amendment notes (https://www.arib.or.jp/english/std_tr/broadcasting/desc/tr-b32.html) describe 1.6 as adding
object-based audio measurement and 1.7 as adding loudness practice outside broadcasting (internet distribution) and
editorial changes; neither mentions the target, the tolerance or the true peak. 1.7's own text wasn't read.

## Question 1: the tolerance's exceptions

Both documents allow exceptions mediaqc can't see in a file: OP-59's programmes "where exact normalisation to target
level is not practicable" (4(c)), and ARIB's programmes made quieter for "creative production requirements" (2.1.6).

Choices:

1. Strict, and say so (recommended): ±1 LU passes, outside it fails; the profiles' documentation and this file name
   the exception, and a delivery that uses it brings a profile file (`basedOn` op-59 or arib-tr-b32, with a wider
   `loudnessPass` or `loudnessWarning`).
2. Outside ±1 LU warns instead of failing.

Answer: 1.

## Question 2: ARIB's source

Choices:

1. 1.5-E1, the latest English edition, is enough (recommended); this file records 1.6's and 1.7's amendment notes and
   that 1.7's text wasn't read, and a Maybe backlog item checks 1.7's chapter 2 if its text is to hand.
2. Keep arib-tr-b32 unconfirmed until 1.7's text is read.

Answer: 1.
