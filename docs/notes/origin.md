# Origin notes

The owner's notes from when the repository was set up (2026-10-31), kept as written down then. Prices and terms are
as noted that day; check them again before relying on them.

## Idea

One report (HTML, JSON, and an exit code for pipelines) of a file's:

- black spans and colour bars, with timecodes;
- line-up tone;
- silences;
- loudness (EBU R128: integrated, range, true peak);
- pixels outside broadcast range;
- crop and letterbox;
- frame types, bitrate and keyframes;
- the shot list.

Each figure says how it is checked against FFmpeg (signalstats, blackdetect, cropdetect, ebur128 and the like).

It builds on the owner's public libraries: ManagedCodecs (pure C# H.264/MJPEG/AAC decoding, NuGet), ShotDetector and
FrameReader (NuGet; FrameReader reads anything through FFmpeg in-process), and the picture and sound measures in
shotdetector-web's core (github.com/bisforboman/shotdetector-web, `src/ShotDetectorWeb.Core`: PictureMeter,
PictureMeasures, Loudness, Mp4Audio; checked against FFmpeg's signalstats, blackdetect, cropdetect and ebur128).

## Market

- Telestream Vidchecker: about $7,950, plus about $1,500 a year maintenance.
- Venera Pulsar: Standard about $12,000; also pay-per-use, and the cloud Quasar.
- Interra Baton: enterprise.
- A newcomer sells a $49/month tier to freelancers.

Positioning: a cheap, local, scriptable first pass for small post houses, freelancers and archives, before files go
to a broadcaster or platform. Not a Netflix/DPP compliance replacement.

## Gaps

- Professional deliveries are often ProRes, DNxHD or MXF, which ManagedCodecs doesn't decode; FrameReader (FFmpeg)
  does.
- Captions, ad markers and platform spec templates are out of scope at first.
- Reference catalogue of checks: EBU's QC test items (tech.ebu.ch/qc).

## Patents

Selling software with an H.264 decoder falls under the Via LA AVC pool: the first 100,000 units a year carry no
royalty, then $0.20 a unit. Check before selling.
