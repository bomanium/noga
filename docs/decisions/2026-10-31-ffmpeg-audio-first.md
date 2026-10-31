# The FFmpeg path's audio measures first

After PR 3 (the v1 profile over `Measurements`), the first Next item, publishing shotdetector-web's measures into
FrameReader, was still in progress in the shotdetector repository, and "Both decoding paths" needs those measures.

Question: what this repository does next, meanwhile.

Choices:

1. `mediaqc check` (recommended): the command, the HTML report and the exit code over `QcReport`, with
   `Measurements` a seam the decoders fill later and tests on hand-built measurements.
2. Loudness via FFmpeg now: start the decoding item with what FrameReader already ships (`Loudness.Measure` over
   ebur128, `ReferenceToneFinder`), so a check gives real loudness and tone on MOV/MXF first.
3. Stop here: wait for FrameReader's release.

Answer: 2. `MediaQc.Ffmpeg` measures on the FFmpeg path with FrameReader 2.18.0: loudness (FFmpeg's ebur128), silences
(FFmpeg's silencedetect, which its FFmpeg build has) and line-up tone (ReferenceTone), each checked against the ffmpeg
command line in a test; black, bars and the start timecode wait for FrameReader.
