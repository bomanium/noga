# First formats and the first check profile

Which open decision to take first: the owner chose this one (of the four in docs/backlog.md), since it settles
the others: it says whether decoding needs FFmpeg, and which of shotdetector-web's measures are needed first.

## Formats

Question: which files the first version reads.

Choices:

1. Pro and web: ProRes and DNxHR in MOV and MXF, and H.264 in MP4 (recommended). This is what the target customers
   deliver, and it needs FFmpeg (FrameReader) in v1, since ManagedCodecs can't decode ProRes or DNxHR.
2. Web only: H.264 and AAC in MP4. All managed, with no LGPL binaries, but it leaves out most broadcast deliveries.
3. Anything FFmpeg opens, with the samples and CI covering the files above.

Answer: 1. Consequence for the decoding decision (still open): FFmpeg through FrameReader is in v1 whatever else is
decided; what is left open is whether ManagedCodecs also decodes H.264 there.

## Check profile

Question: what the first check profile contains.

Choices:

1. EBU R128 loudness (integrated, range, true peak against -23 LUFS and -1 dBTP), black spans, colour bars, line-up
   tone and silences, with timecodes (recommended). Each one is checked against ebur128, blackdetect or silencedetect.
2. That plus pixels outside broadcast range (signalstats) and crop/letterbox (cropdetect).
3. Loudness only.

Answer: 1. Range and crop come after it.
