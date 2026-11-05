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

- Publish the measures from shotdetector-web's core into FrameReader
  - Why: docs/decisions/2026-10-31-where-the-measures-live.md: one implementation, checked against FFmpeg in the
    public repositories, fed by any frame source. What FrameReader has already (2026-10-31): `Loudness.Measure` (FFmpeg's
    own ebur128 filter over an `AudioReader`, so the FFmpeg path only) and `ReferenceToneFinder` (line-up tone, any
    samples). What only shotdetector-web's core has: PictureMeter/PictureMeasures (black, bars, range, crop, frame
    types, on decoded frames), a managed BS.1770 loudness and true peak, silences and channel peaks (Mp4Audio). Those
    go public, taking frames and samples from either decoder. The work is in the shotdetector and shotdetector-web
    repositories (their own PRs and releases); media-qc then takes the packages
  - Status: Done (2026-11-01: FrameReader 2.19.0 has PictureMeter/PictureMeasures and SilenceFinder beside
    LoudnessMeter and ReferenceToneFinder; media-qc uses them on both paths)

- The v1 profile in MediaQc.Core: verdicts, thresholds, timecodes
  - Why: docs/decisions/2026-10-31-first-formats-and-check-profile.md: EBU R128 (integrated -23 LUFS, true peak
    -1 dBTP, loudness range), black spans, bars, tone and silences, each a pass/warning/failure with timecodes and
    the FFmpeg filter it is checked against. Thresholds: decisions/2026-10-31-v1-thresholds.md; exit codes, head and
    tail, timecode: decisions/2026-10-31-exit-codes-head-tail-timecode.md
  - Status: Done (2026-11-01: every v1 check measured on both paths and judged, with its timecodes and what it is
    checked against)

- Both decoding paths, with a test that they agree
  - Why: docs/decisions/2026-10-31-decoding.md: ManagedCodecs for H.264/AAC MP4, FrameReader (FFmpeg) for the rest;
    the same MP4 through both gives the same measures
  - Status: Done (2026-11-01, decisions/2026-11-01-range-and-crop.md: black, bars, range and crop through PictureMeter
    on both paths; SampleTests check both agree on make-samples.sh's MP4, the picture exactly, and each figure against
    ffmpeg)

- Pixels outside broadcast range and crop/letterbox
  - Why: The next checks after the first profile (decisions/2026-10-31-first-formats-and-check-profile.md); shotdetector-web
    already measures both (signalstats, cropdetect), and make-samples.sh makes files for them
  - Status: Done (2026-11-01, decisions/2026-11-01-range-and-crop.md: a warning over 1% of a frame (EBU R 103), bars
    left out; crop as information)

- Packaging: a release of `mediaqc` with FFmpeg's licence files beside its libraries
  - Why: CLAUDE.md's licence rule: FrameReader.Native's LGPL libraries ship separate and replaceable, with the LGPL
    text, the build's configure line and its SBOM beside them (the package has them: COPYING.*, configure.txt,
    sbom.spdx.json); nothing copies them into a build's output yet. Also which platforms (linux-x64 and win-x64 so
    far) and the form (a self-contained folder per platform, a zip)
  - Status: Done (2026-11-01, decisions/2026-11-01-packaging.md: tools/publish.sh builds self-contained linux-x64
    and win-x64 folders and archives into artifacts/, MediaQc.Cli.csproj's PublishLicences puts FFmpeg's licence,
    configure line, SBOM and README in licences/ffmpeg/, .NET's in licences/dotnet/, packaging/'s notices and README
    beside them; CI publishes both and runs the Linux one on a sample)

- Frame types, keyframes and bitrate
  - Why: The description promises them in the report (decisions/2026-11-01-frames-bitrate-shots.md)
  - Status: Done (2026-11-01: each frame's picture type from PictureMeter, the keyframes and bytes a second from the
    video packets (av_read_frame on the FFmpeg path, ManagedCodecs' demuxer on the managed one), as information,
    each checked against ffprobe -show_frames and -show_packets exactly, on both paths)

- The shot list: ShotDetector's shots in the report, on both paths
  - Why: The description promises the shot list (decisions/2026-11-01-frames-bitrate-shots.md)
  - Status: Done (2026-11-01: PySceneDetect detect-adaptive's shots through ShotDetector 3.25.0, trusted as checked
    in its own CI; the FFmpeg path detects on the file, the managed path on ManagedCodecs' frames (decoded once for
    the picture measures and the shots) converted by ShotDetector.FastYuv, shipped as its own LGPL assembly with its
    licence; a "shots" finding and one per shot, in the reports; both paths agree on the MP4)

- A release workflow: on a tag, CI publishes the archives on a GitHub release
  - Why: A release was built by hand (tools/publish.sh) and CI only checked it builds and runs
  - Status: Done (2026-11-01, decisions/2026-11-01-release-workflow.md: a `v*` tag that matches Version runs CI,
    then publishes the Linux and Windows archives at once, a pre-release for a suffixed version)

- A local web UI: `mediaqc serve`
  - Why: decisions/2026-10-31-product-shape.md kept it for later, on the same core
  - Status: Done (2026-11-01, decisions/2026-11-01-web-ui.md: ASP.NET Core minimal APIs and one page on 127.0.0.1;
    files picked in the server's folders and checked where they are, one at a time, shown live by server-sent events,
    the reports beside each file as the CLI writes them)

- A watch folder: `mediaqc serve --watch FOLDER`
  - Why: Post houses hand files over through folders; checking each as it lands needs nobody at the page
  - Status: Done (2026-11-01, decisions/2026-11-01-watch-folder.md: the folder and its subfolders scanned every 2 s,
    media files without a report checked once their copy has settled for 5 s, shown on the page like picked ones)

- Open films in CI
  - Why: Generated files have known answers but not real pictures; Blender's open films, downloaded and pinned by
    SHA-256, check the measures on real material
  - Status: Done (2026-11-01, decisions/2026-11-01-open-films.md: Big Buck Bunny and Tears of Steel, every figure
    against the ffmpeg command line, weekly and on demand (films.yml); range spans under 1 s apart now join, which the
    first run on Big Buck Bunny showed was needed)

- Every audio track measured and judged, each with its own findings
  - Why: The report judged the first track only (decisions/2026-11-01-open-films.md); deliveries often carry stereo
    and 5.1, or split stems
  - Status: Done (2026-11-01, decisions/2026-11-01-every-audio-track.md: FrameReader 2.21.0's
    `AudioReaderOptions.Stream` (bisforboman/shotdetector#697); each track measured from its own line-up and judged by
    the same profile, its findings marked with the track and compared with ffmpeg -map 0:a:N; files with several audio
    tracks go through the FFmpeg path)

- Map the checks to EBU's QC test items
  - Why: EBU's catalogue (qc.ebu.io) names and defines the checks broadcasters know; reports that use its names and
    IDs read as familiar
  - Status: Done (2026-11-01, decisions/2026-11-01-ebu-qc-items.md: each finding carries its item, read from
    qc.ebu.io's API, in the JSON and as a linked column in the HTML report; bitrate and the shot list have none)

- A shared-machine mode for `mediaqc serve`
  - Why: A team's shared machine runs one server for everyone; decisions/2026-11-01-web-ui.md kept it for later
  - Status: Done (2026-11-01, decisions/2026-11-01-shared-machine.md: `--listen` beyond localhost needs `--root`
    folders, the only ones seen or checked, links that lead out refused; a token link sets an HttpOnly cookie every
    request needs; plain HTTP for trusted networks)

- Platform spec profiles
  - Why: Deliveries outside EBU R128 territory have their own loudness and peak specs (docs/notes/origin.md's "platform
    spec templates")
  - Status: Done (2026-11-01, decisions/2026-11-01-profiles.md: atsc-a85, op-59 and arib-tr-b32 beside ebu-r128, and
    profile files (JSON, basedOn a shipped one), chosen with --profile on check and serve)

- A desktop app
  - Why: A native window with drag and drop of real file paths, beside the browser page
  - Status: Done (2026-11-01, decisions/2026-11-01-desktop-app.md: mediaqc-app, Avalonia 12, in the same release
    folder; Checker and Jobs shared through src/MediaQc.Checking; tested headless and run on Xvfb)

- OP-59's and ARIB TR-B32's figures checked against the originals
  - Why: decisions/2026-11-01-profiles.md took them from vendor summaries (ARIB's tolerance not found, ±1 LU assumed);
    a customer relying on op-59 or arib-tr-b32 needs them confirmed
  - Status: Done (2026-11-02, decisions/2026-11-02-op59-arib-figures.md: OP-59 Issue 4 (2018) and ARIB TR-B32 1.5-E1
    (2016) give the figures mediaqc ships, ARIB's ±1 included; their exceptions are left to profile files)

- The desktop app's icon and Linux desktop entry
  - Why: mediaqc-app shipped without an icon, and Linux had no menu entry
  - Status: Done (2026-11-02: decisions/2026-11-02-app-icon.md, 2026-11-02-app-icon-wiring.md)

- Installers for the desktop app: an NSIS setup.exe for Windows and an AppImage for Linux, the archives kept
  - Status: Done (2026-11-02, decisions/2026-11-02-installers.md)

- HTTPS for a shared server: --cert's certificate, else a self-signed one kept in the profile; --http opts out
  - Status: Done (2026-11-03, decisions/2026-11-03-shared-https.md)

- A Linux arm64 release: the folder, its tar.gz and an aarch64 AppImage, checked under qemu against x64's findings
  - Status: Done (2026-11-03, decisions/2026-11-03-linux-arm64.md)

- The delivery format: reported for every file, judged against a profile file's "format", with three templates
  - Status: Done (2026-11-03, decisions/2026-11-03-delivery-format.md)

- Per-channel audio faults: a dead or silent channel, a pair out of phase, clipping, dual mono
  - Status: Done (2026-11-03, decisions/2026-11-03-channel-faults.md)

- Freeze frames, judged like black, black and bars left out
  - Status: Done (2026-11-03, decisions/2026-11-03-freeze-frames.md)

- The line-up tone's level, and the head and tail (bars, tone, black before and after) judged
  - Status: Done (2026-11-03, decisions/2026-11-03-line-up-and-head-tail.md)

- Interlacing from the picture (idet), against the stream's field order
  - Status: Done (2026-11-03, decisions/2026-11-03-interlacing.md)

- A launch README: what Noga checks and how each figure is checked against FFmpeg, install, `noga check` and its exit
  codes, screenshots of the report, the web UI and the app, an example report
  - Why: the root README is the whole pitch once the repository is public, and it had 14 lines; the user's text only
    shipped inside the release (decisions/2026-11-03-launch-readiness.md)
  - Status: Done (2026-11-03: README.md rewritten, with docs/example/'s reports and docs/images/'s screenshots; the
    report's "checked against" column now wraps, as its filter strings made the page wider than the window)

- Robustness on odd files: generated broken and unusual files (truncated, no moov, variable frame rate, audio-only,
  video-only, 10-bit 4:2:2 and 4K, HDR-tagged, several video streams), each a clean report or a clear read error
  (exit 2), never an unhandled exception
  - Why: small shops get messy files, and a crash on the first one costs trust; no test reads a broken file yet
    (decisions/2026-11-03-launch-readiness.md)
  - Status: Done (2026-11-04: 48 odd files checked, decisions/2026-11-04-odd-files.md: the crashes and the queue
    fixed, damaged MP4s to the FFmpeg path, `OddFileTests` on 30 of them; the `decode` check fails a picture or sound
    that ends early, or frames that don't decode; sound-only files are checked for their sound)

- One status style for the report, the web UI and the app
  - Why: the 2026-11-04 UI/UX review (decisions/2026-11-04-report-layout.md): tinted badges in the report and the web
    UI, solid ones in the app; dark mode only in the app and now the report; "Failure", "Failed" and "Warnings" by
    surface, and the CLI prints "Info" as a passing file's verdict; the app's dark-mode error text and Failed badge
    are below WCAG AA. One set of colours and labels (light and dark) in Noga.Core, used by all three, and a neutral
    style for "Couldn't read"
  - Status: Done (2026-11-04: `StatusStyle`, decisions/2026-11-04-status-style.md)

- Job rows that explain themselves, in the web UI and the app
  - Why: the same review: a row says "Failed" without why, repeats the full path, and a read error repeats it twice;
    the web UI's badges don't line up and only it shows the elapsed time. A one-line reason (the first failure or
    warning), the folder instead of the path, a short error, elapsed time in both, "Report" as the main action
  - Status: Done (2026-11-04: `QcReport.Reason`, `Job.ShortError`, decisions/2026-11-04-job-rows.md)

- The web UI's usability gaps
  - Why: the same review: folders and ".." aren't keyboard-reachable (spans), a bad path leaves the old listing under
    it, Check gives no feedback and queues duplicates, non-media files get a Check button, no "check all in this
    folder" (its dark mode and its grey text's contrast came with decisions/2026-11-04-status-style.md)
  - Status: Done (2026-11-04: decisions/2026-11-04-web-ui-gaps.md)

- Raw values in findings
  - Why: the same review: a silent file's true peak reads "-Infinity dBTP" and passes; the format line shows ffprobe's
    unknown colour values as "?/?/bt709"
  - Status: Done (2026-11-04: `VideoFormat.Colour`, decisions/2026-11-04-raw-values.md)

- The app's drop zone and narrow window
  - Why: the same review: the drop zone always takes room and doesn't highlight on drag-over; at a narrow width the
    hint is cut mid-word and file names clip without an ellipsis
  - Status: Done (2026-11-04: decisions/2026-11-04-app-drop-zone.md)

- The report and the JSON say when the file was checked and which file it was
  - Why: the second UI/UX review (decisions/2026-11-04-rows-in-place.md): a report passed on to a broadcaster can't
    show which version of the file it covers: no check date, full path, size or modification time
  - Status: Done (2026-11-04: `CheckedFile`, decisions/2026-11-04-checked-file.md)

- A long check's progress in the web UI and the app
  - Why: the same review: a 60-minute ProRes master shows "measuring N s" for about 17 minutes, nothing more
  - Status: Done (2026-11-04: `CheckProgress`, `Job.Progress`, decisions/2026-11-04-progress.md)

- The report's format line in plain words, and print fixed
  - Why: the same review: ffprobe's words in front of clients ("mov,mp4,m4a,3gp,3g2,mj2", "yuv422p10le", "Format:");
    in print the timecodes overrun the To column, the shot list never prints, a point shows an empty To
  - Status: Done (2026-11-04: `FormatWords`, decisions/2026-11-04-format-words.md)

- Small layout fixes from the second review
  - Why: the same review: the web UI's empty "Checks" text squeezed into the badge column, its path field refusing a
    file; the app's narrow-window buttons against the badge, its empty window mostly blank, no "Add folder…"; both
    showing the start of a deep folder rather than its end; "Couldn't read" in the same grey as "Queued"
  - Status: Done (2026-11-04: decisions/2026-11-04-layout-fixes.md)

- Speed on long files: seconds per minute of footage and peak memory, measured on the open films (films.yml), the
  worst fixed, the figure in the README
  - Why: deliveries run 30 to 90 minutes of ProRes HQ, and nothing says how long a check takes or what memory it
    needs (decisions/2026-11-03-launch-readiness.md)
  - Status: Done (2026-11-04: `tools/benchmark.sh --films`. The open films check at 5-14 s a minute with flat memory,
    and nothing there needed fixing. The worst problem found, memory that grew with a QuickTime PCM file's length, was
    fixed first. The figures are in the README and docs/notes/performance.md. Also two checks at a time in `Jobs`,
    1.76x sooner: decisions/2026-11-04-checks-side-by-side.md)

- Faster single checks: measure the picture pass while the next frame is decoded; take the shots from the picture
  pass's frames on the FFmpeg path too, rather than from a second decode
  - Why: docs/notes/performance.md: a check keeps about 2 of 4 cores busy. PictureMeter runs on one thread (40% of the
    time), and the shots decode the video again (20%). A 1-hour ProRes HQ master takes about 17-25 minutes on a
    4-core machine. Two checks at a time is done (decisions/2026-11-04-checks-side-by-side.md), but it doesn't help
    one long file
  - Status: Done (2026-11-05: the meters on threads of their own, PictureMeter on two, and the picture pass, the
    shots and the sound side by side: ProRes HQ 1.85x faster, H.264 1.2-1.4x, the reports the same;
    decisions/2026-11-05-faster-single-checks.md. The shots from the picture pass's frames can't take ProRes HQ, and
    stay in Maybe for the formats they can)
