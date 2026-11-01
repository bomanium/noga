# The start timecode through ffprobe, for now

After PR 8 (the managed path), black, bars and the start timecode were left, all waiting on FrameReader: neither it
nor ManagedCodecs gives a file's tags (MOV and MP4's tmcd track, MXF's timecode) yet, so every report counted from
00:00:00:00.

Question: what this session takes next.

Choices:

1. Stop here (recommended): wait for FrameReader's picture measures.
2. The start timecode through an external ffprobe for now, replaced by FrameReader later.
3. Report polish: which decoder measured the file, and its facts, in both reports.

Answer: 2. Both paths ask the ffprobe on PATH (`ffprobe -show_entries format_tags=timecode:stream_tags=timecode`):
the video stream's tag (MOV, MP4), else the container's (MXF), else any stream's, when it is a timecode at the file's
rate. Nothing of ffprobe is shipped; without it the report says the timecode wasn't read (a `timecode` finding) and
counts from 00:00:00:00. make-samples.sh's programme starts at 10:00:00:00, and the tests check both paths read it.
