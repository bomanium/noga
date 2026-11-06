# Backlog

What Noga does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

## Waiting

Nothing waiting.

## Maybe

- Noga's meters and a packet reader published into FrameReader, in one release, and Noga switched over
  - Why: 2026-10-31-where-the-measures-live.md puts the measures in the public packages; FreezeMeter, InterlaceMeter,
    ChannelMeter, LoudnessBlocks, the timestamp gaps and Packets.Read (FFmpeg.AutoGen directly) were written here.
    One release, not one per meter; the verdicts stay (decisions/2026-11-05-meters-upstream.md)
  - Status: Maybe

- Dropped frames filled by a repeat: single frames identical to the one before
  - Why: capture cards and encoders often fill a dropped frame by repeating the last, which the timestamps don't
    show and freeze (2 s at least) misses. FreezeMeter's arithmetic with a duration of one frame, checked against
    mpdecimate or freezedetect with a short d; animation, slates and pulldown repeat frames on purpose, so it needs
    care (a cadence, or only isolated repeats in moving picture) before it can be more than indicative
  - Status: Maybe

- A WAV cut short caught: its RIFF header's data size against the samples there are
  - Why: FFmpeg gives a WAV's duration from what is in the file, so decode.sound passes a stem cut short
    (decisions/2026-11-04-odd-files.md)
  - Status: Maybe

- The shots from the picture pass's frames on the FFmpeg path, for the formats ShotDetector's frame source takes
  (yuv420p, yuv422p and their full-range forms, yuv420p10le: DNxHR SQ, H.264 in MOV or with several audio tracks)
  - Why: the shots decode the video a second time, the largest piece of work left in a check now that the meters run
    side by side (docs/notes/performance.md); ProRes HQ (yuv422p10le) can't, so it would not speed up the main case
  - Status: Maybe

- AS-11 UK metadata in MXF (ffprobe's tags); flashing (PSE) only as an indicative warning, as FFmpeg has nothing to
  check it against
  - Why: the rest of the 2026-11-03 review's delivery checks; the owner took the others first
    (decisions/2026-11-05-av-lengths.md)
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
