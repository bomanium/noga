# What next: launch readiness

Asked 2026-11-03, when Next was empty: the report had every check the description names, and the Waiting item (the
repository renamed and public) was the owner's. An independent review of the repository (a second session, given the
code and docs, not a view) found the checks ahead of what a public repository needs: a 14-line root README (the user's
text, `packaging/README.txt`, only ships inside the release), no tests on broken or unusual files, no figure for how
fast a check runs, version 0.1.0-alpha.2 and no tag.

## What goes into Next first

- **A launch README** (recommended): the root README rewritten for going public: what Noga checks and how each figure
  is checked against FFmpeg, install from the release, `noga check` with its exit codes, screenshots of the report,
  the web UI and the app, and an example report from make-samples.sh's programme (generated, not media). Then
  robustness on odd files.
- Robustness on odd files: generated broken and unusual files (truncated, no moov, variable frame rate, audio-only,
  10-bit 4K, HDR-tagged, several video streams), each a clean report or a clear exit 2, never a crash.
- Speed on long files: seconds per minute of footage and peak memory on the open films, the worst fixed, the figure in
  the README.
- More checks from the Maybe list (short-term and momentary loudness, audio and video durations, ...).

**Answer: a launch README.** The other two launch items follow it in Next, robustness first; the more checks stay
Maybe. A v1.0 (the JSON report's schema frozen with a version, a changelog, the tag) is for after the repository is
public, and the owner's.
