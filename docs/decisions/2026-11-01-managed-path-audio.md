# The managed path's audio measures next

After PR 7 (CI checks the samples against FFmpeg), what was left on the backlog needed FrameReader changes in the
shotdetector repository: black and bars, the start timecode, and with them the rest of the decoding item.

Question: what this session takes next.

Choices:

1. Stop here (recommended): wait for FrameReader's picture measures.
2. The start timecode through an external ffprobe, to be replaced by FrameReader later.
3. The ManagedCodecs path: H.264/AAC MP4 with ManagedCodecs 0.4.0 for loudness, silences and tone, ahead of the
   picture measures.

Answer: 3. `MediaQc.Managed` demuxes and decodes the AAC with ManagedCodecs; loudness and tone are FrameReader's
managed meters (LoudnessMeter, ReferenceToneFinder: no FFmpeg), silences Core's `SilenceFinder`, a port of
silencedetect's algorithm (to move into FrameReader with the other measures). `mediaqc check` uses the path for
H.264/AAC MP4, and a test checks that both paths give the same figures on make-samples.sh's MP4.
