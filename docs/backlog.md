# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

Nothing: the report has every check the description names, a release workflow, a local web UI with a watch folder, and weekly checks on open films. What comes next
is the owner's to choose (Maybe below).

## Waiting

Nothing.

## Maybe

- Per-channel audio faults: a dead or missing channel, dual mono sent as stereo, channels out of phase, clipping
  - Why: silence today needs every channel silent, so a file with a dead channel passes; the review of 2026-11-03
    (decisions/2026-11-03-delivery-format.md) put it second. Checked against astats (per channel), aphasemeter, and
    silencedetect on each channel (channelsplit)
  - Status: Maybe

- Freeze frames, judged like black (a warning mid-programme)
  - Why: a standard QC item and a common sign of a bad render; freezedetect, each end within one frame. Third in the
    2026-11-03 review
  - Status: Maybe

- Bars and tone, head and tail judged: the tone's level (-18 dBFS EBU, -20 dBFS US), what a spec wants at the head
  and tail (bars and tone, slate, black, silence)
  - Why: they are reported today, never judged; profile fields like the format's
  - Status: Maybe

- More from the 2026-11-03 review: interlacing detected from the picture (idet, against field_order), short-term and
  momentary loudness limits and a loudness-range window, constant-colour frames (EBU 0016B), RGB gamut (EBU R 103),
  audio and video durations that differ, AS-11 UK metadata in MXF (ffprobe's tags); flashing (PSE) only as an
  indicative warning, as FFmpeg has nothing to check it against
  - Why: each is a common delivery check; most can be checked against an FFmpeg filter, the review's notes say how
  - Status: Maybe

- The desktop app's next steps: macOS (osx-arm64, signed and notarised), code signing on Windows
  - Why: decisions/2026-11-01-desktop-app.md shipped mediaqc-app in the release folders, unsigned; the icon, a Linux
    desktop entry (decisions/2026-11-02-app-icon.md, 2026-11-02-app-icon-wiring.md) and installers (a setup.exe and an
    AppImage: decisions/2026-11-02-installers.md) are done
  - Status: Maybe

- A macOS (osx-arm64) release
  - Why: FrameReader.Native has an osx-arm64 package; CI can't run it (ubuntu only, macOS minutes cost 10x, and qemu
    doesn't run macOS binaries), and macOS wants a signed, notarised binary. Linux arm64 is done
    (decisions/2026-11-03-linux-arm64.md)
  - Status: Maybe

- Code signing for the Windows build
  - Why: an unsigned mediaqc.exe or setup.exe from a download is blocked or warned about by SmartScreen; signing would
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
