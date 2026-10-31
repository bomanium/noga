# Where the measures live

shotdetector-web's core (`src/ShotDetectorWeb.Core`: PictureMeter and PictureMeasures, Loudness, Mp4Audio's silences
and peaks) measures black, bars, range, crop, loudness and silence, checked against FFmpeg's signalstats, blackdetect,
cropdetect, ebur128 and silencedetect. It runs on frames and samples that ManagedCodecs decodes from MP4 and AVI.

Question: where the measures live, so media-qc can use them on ProRes/DNxHR/MXF too.

Choices:

1. Published from shotdetector-web's core into the public FrameReader/ShotDetector packages, fed by any frame source
   (recommended). One implementation, tested against FFmpeg in the public repositories; it costs a public API
   designed and released there first.
2. Copied here (MIT allows it; keep the notice). The fastest start, but the two copies drift apart.
3. A private package here, with shotdetector-web moving to it. Not possible while shotdetector-web is public.

Answer: 1. media-qc takes them as package dependencies (MIT); this repository adds the QC verdicts, the profile, the
report and the UI.
