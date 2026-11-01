# Black, bars, range and crop with FrameReader 2.19.0

FrameReader 2.19.0 came out with the picture measures (PictureMeter: blackdetect's, signalstats' BRNG, cropdetect's,
the colour bars and each frame's picture type, from frames the caller decodes) and a SilenceFinder of its own.

Question 1: whether to move to it and measure black and bars on both decoding paths.

Choices:

1. Yes, black and bars (recommended): completes v1's checks.
2. Also range and crop: the same, plus pixels outside broadcast range and crop/letterbox (Maybe items, not in v1).
3. Not now.

Answer: 2.

Question 2: how pixels outside broadcast range (signalstats' BRNG, luma 16-235 and chroma 16-240) are judged.

Choices:

1. EBU R 103's 1% (recommended): a warning for each span of frames with more than 1% of the pixels out of range,
   with the worst frame's share; fewer is a pass.
2. Any pixel warns.
3. Information only.

Answer: 1. Frames with colour bars aren't judged: bars' PLUGE is below black on purpose.

Question 3: how crop/letterbox (cropdetect) is judged.

Choices:

1. Information only (recommended): the active picture (crop=w:h:x:y and its aspect ratio), no verdict, as
   letterboxing is often intended.
2. A warning whenever the active picture is smaller than the frame.

Answer: 1.

With the code: both paths feed PictureMeter, the FFmpeg path FFmpeg's frames (FrameDecoder), the managed path
ManagedCodecs' H.264 frames, with the colour range and rotation from ProbeJson. Core's port of silencedetect gave way to
FrameReader's SilenceFinder. A black span that runs to the end ends after the last frame, where blackdetect says the
last frame's pts (a FIXME in FFmpeg), so the tail counts as the tail. A file that is all line-up tone is measured whole
for loudness. The tests check each figure against blackdetect, signalstats and cropdetect on make-samples.sh's files,
and that both paths give the same picture measures on its MP4.
