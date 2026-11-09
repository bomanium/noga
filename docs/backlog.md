# Backlog

What Noga does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

- A book as one check: `noga check FOLDER` checks a folder's files as one set, each file's report plus one summary
  (every file the same format, mono or stereo throughout, levels within a tolerance of each other) and one exit code
  - Why: ACX judges an audiobook as a set of files as well as one by one; the owner chose a folder given to `noga
    check` over a new command (decisions/2026-11-09-set-check.md)
  - Status: Open

- Room tone at the head and tail of a spoken-word file: quiet, but not digital silence, for 1 to 5 s (ACX's figures;
  its blog says 0.5 to 1 s at the head), judged under acx
  - Why: ACX asks for it in each file (decisions/2026-11-09-acx-profile.md); silences and each channel's RMS are
    measured already, so it needs a profile threshold and a reading of where the first and last words are
  - Status: Open

## Waiting

- The noise floor under acx: FrameReader's meter for the RMS of a track's quietest 500 ms (digital silence left out),
  then `Profile.MaxNoiseFloor` (-60 dB under acx) judged as `level.noise-floor`, checked against astats' per-window
  RMS
  - Why: ACX asks for a noise floor no higher than -60 dB RMS, and astats' own "Noise floor" is a 50 ms peak, -∞ at
    any digital silence; the owner chose a new meter, which belongs in FrameReader (decisions/2026-11-09-noise-floor.md)
  - Status: Waiting (on the meter in FrameReader, bisforboman/FrameReader, and its release)

- LoudnessBlocks switched to FrameReader's LoudnessMeter (MaxMomentary(At), MaxShortTerm(At)), once a FrameReader
  release counts only the blocks whose 400 ms or 3 s window is full (none filled: no time, Noga's null, where 2.25.0
  gives -120.691 LUFS); its last digits accepted, as they are FFmpeg's f_ebur128.c arithmetic and Noga's aren't
  - Why: the last of Noga's meters after the switch to FrameReader's; the owner chose to fix a meter that differs
    upstream and wait (decisions/2026-11-07-switching-to-framereaders-meters.md), and only the unfilled windows when
    FrameReader's digits turned out to be FFmpeg's (decisions/2026-11-08-loudest-blocks-upstream.md)
  - Status: Waiting (on the fix in FrameReader, now its own repository, bisforboman/FrameReader, and its release)

## Maybe

- The shots from the picture pass's frames on the FFmpeg path, for the formats ShotDetector's frame source takes
  (yuv420p, yuv422p and their full-range forms, yuv420p10le: DNxHR SQ, H.264 in MOV or with several audio tracks)
  - Why: the shots decode the video a second time, the largest piece of work left in a check now that the meters run
    side by side (docs/notes/performance.md); ProRes HQ (yuv422p10le) can't, so it would not speed up the main case
  - Status: Maybe

- AS-11 UK metadata in MXF; flashing (PSE) only as an indicative warning, as FFmpeg has nothing to check it against
  - Why: the rest of the 2026-11-03 review's delivery checks; the owner took the others first
    (decisions/2026-11-05-av-lengths.md). FFmpeg's MXF reader skips descriptive metadata, so ffprobe gives none of
    AS-11's fields: it would be read by Noga and checked against MediaInfo, on files made with BBC's bmx
    (decisions/2026-11-06-as-11-parked.md)
  - Status: Maybe (parked 2026-11-06)

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
