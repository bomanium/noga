# Frame types, keyframes, bitrate and the shot list

With v1's checks and packaging done, the owner chose what comes next.

Question 1: what next after v1.

Choices:

1. The remaining checks (recommended): frame types, bitrate and keyframes, and the shot list, which the description
   promises in the report, on both decoding paths.
2. A local web UI (`mediaqc serve`).
3. A release workflow.
4. Stop here for now.

Answer: 1. The UI stays a later option (2026-10-31-product-shape.md), to show the full report.

Question 2: how the new figures are judged.

Choices:

1. Information only (recommended): the picture types and GOP, the keyframes and their longest interval, the average and
   peak bitrate a second, and the shot list; platform limits come with spec templates later.
2. Also a warning when keyframes are more than 2 s apart.
3. Also a warning when a second's bitrate is over twice the average.

Answer: 1.

Question 3: how the managed path (H.264 in MP4) gets its shot list. ShotDetector takes a caller's frames only with a
YUV converter, ShotDetector.FastYuv's (LGPL-2.1-or-later, a managed assembly).

Choices:

1. ShotDetector decodes through FFmpeg's libraries on both paths (recommended): no new LGPL binary, the managed path
   decodes the video twice.
2. ManagedCodecs' frames into ShotDetector with FastYuv, shipped as a separate, replaceable DLL with its licence beside
   it (CLAUDE.md's rule for LGPL).
3. Not on the managed path yet (reported as not measured).

Answer: 2.

Question 4: what the shot list is checked against. FFmpeg has no detector that gives PySceneDetect's shots (scdet
scores otherwise).

Choices:

1. PySceneDetect's adaptive detector with its defaults, which ShotDetector matches exactly in its own CI; this
   repository's tests check the known cuts of make-samples.sh's programme (recommended).
2. As 1, and CI here also runs scenedetect (pip, OpenCV) on the samples.
3. FFmpeg's scdet instead.

Answer: 1, without the known-cuts test: ShotDetector is the owner's own library and checked against PySceneDetect
there, so it is trusted here ("I don't think we need to check shots, we adapt ShotDetector to do that and will have
to trust it"). The report says the shots are PySceneDetect detect-adaptive's through ShotDetector. The test that both
decoding paths give the same figures covers the shot list too: it checks this repository's plumbing (ManagedCodecs'
frames through FastYuv), not ShotDetector.

With the code: picture types are each decoded frame's (ffprobe -show_frames' pict_type), keyframes and bitrate the
video packets' (ffprobe -show_packets' flags, pts and size; the FFmpeg path reads them with av_read_frame, the managed
path from ManagedCodecs' demuxer), each checked against the ffprobe command line exactly.
