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
