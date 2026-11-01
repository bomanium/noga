# The start timecode

After PR 8 (the managed path), black, bars and the start timecode were left, and every report counted from
00:00:00:00: the timecode tag (MOV and MP4's tmcd track, MXF's timecode) wasn't read.

Question 1: what this session takes next.

Choices:

1. Stop here (recommended): wait for FrameReader's picture measures.
2. The start timecode through an external ffprobe for now, replaced by FrameReader later.
3. Report polish: which decoder measured the file, and its facts, in both reports.

Answer: 2. Built that way first (PR 9, before merging), on the belief that FrameReader didn't give a file's tags.
The owner asked why not FrameReader: its `MediaProbe.ProbeJson` (2.18.0) is ffprobe's `-show_format -show_streams`
JSON in-process, tags included, which had been missed.

Question 2: how the timecode is read, given ProbeJson; on the managed path it means loading FFmpeg's libraries just to
read the tag.

Choices:

1. ProbeJson on both paths (recommended): the libraries ship anyway for MOV/MXF; the managed path's decoding stays
   managed and only the tag is read through them.
2. ProbeJson on the FFmpeg path; the managed path reads MP4's tmcd track itself, free of native code (more code;
   ManagedCodecs doesn't parse tmcd yet).
3. Keep the external ffprobe.

Answer: 1. `TimecodeTag.Pick` (Core) takes the video stream's tag, else the container's, else any stream's, when it is
a timecode at the file's rate; `StartTimecode.Add` (MediaQc.Ffmpeg) gets the JSON from ProbeJson for both paths. When
FFmpeg's libraries don't load, the report says the timecode wasn't read and counts from 00:00:00:00. make-samples.sh's
programme starts at 10:00:00:00; the tests check both paths read it and that it is the ffprobe command line's tag.
