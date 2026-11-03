# Backlog

What Noga does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

- Robustness on odd files: generated broken and unusual files (truncated, no moov, variable frame rate, audio-only,
  video-only, 10-bit 4:2:2 and 4K, HDR-tagged, several video streams), each a clean report or a clear read error
  (exit 2), never an unhandled exception
  - Why: small shops get messy files, and a crash on the first one costs trust; no test reads a broken file yet
    (decisions/2026-11-03-launch-readiness.md)
  - Status: Open

- Speed on long files: seconds per minute of footage and peak memory, measured on the open films (films.yml), the
  worst fixed, the figure in the README
  - Why: deliveries run 30 to 90 minutes of ProRes HQ, and nothing says how long a check takes or what memory it
    needs (decisions/2026-11-03-launch-readiness.md)
  - Status: Open

## Waiting

- The repository renamed and public, and the name's last checks
  - Why: decisions/2026-11-03-name-noga.md: the code is renamed Noga; the owner renames the GitHub repository to
    bomanium/noga (Settings, General), checks noga.* domains and the trademark registers (EUIPO, USPTO: NOGA in
    classes 9 and 42), then makes it public (decisions/2026-11-03-mit-licence.md)
  - Status: Waiting (on the owner)

## Maybe

- More from the 2026-11-03 review: short-term and momentary loudness limits and a loudness-range window,
  constant-colour frames (EBU 0016B), RGB gamut (EBU R 103),
  audio and video durations that differ, AS-11 UK metadata in MXF (ffprobe's tags); flashing (PSE) only as an
  indicative warning, as FFmpeg has nothing to check it against
  - Why: each is a common delivery check; most can be checked against an FFmpeg filter, the review's notes say how
  - Status: Maybe

- The desktop app's next steps: macOS (osx-arm64, signed and notarised), code signing on Windows
  - Why: decisions/2026-11-01-desktop-app.md shipped noga-app in the release folders, unsigned; the icon, a Linux
    desktop entry (decisions/2026-11-02-app-icon.md, 2026-11-02-app-icon-wiring.md) and installers (a setup.exe and an
    AppImage: decisions/2026-11-02-installers.md) are done
  - Status: Maybe

- A macOS (osx-arm64) release
  - Why: FrameReader.Native has an osx-arm64 package; CI can't run it (ubuntu only, macOS minutes cost 10x, and qemu
    doesn't run macOS binaries), and macOS wants a signed, notarised binary. Linux arm64 is done
    (decisions/2026-11-03-linux-arm64.md)
  - Status: Maybe

- Code signing for the Windows build
  - Why: an unsigned noga.exe or setup.exe from a download is blocked or warned about by SmartScreen; signing would
    also allow an MSIX (decisions/2026-11-02-installers.md)
  - Status: Maybe

- The AVC patent pool before selling
  - Why: Software with an H.264 decoder falls under the Via LA AVC pool (the first 100,000 units a year carry no
    royalty, then $0.20 a unit). Check the current terms, and whether decoding through FFmpeg's libraries changes it,
    before the first sale (docs/notes/origin.md)
  - Status: Maybe

- Captions and ad markers
  - Why: Out of scope at first (docs/notes/origin.md); what customers ask for after the first version decides which
  - Status: Maybe

- ARIB TR-B32 1.7's chapter 2 checked against arib-tr-b32
  - Why: decisions/2026-11-02-op59-arib-figures.md read the English edition, 1.5-E1 (2016); 1.7 (2026-09-10) is in
    Japanese and sold to non-members, and its amendment notes don't mention changing the target, tolerance or true peak
  - Status: Maybe
