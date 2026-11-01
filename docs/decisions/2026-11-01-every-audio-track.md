# Every audio track

Question 1: after the open films (and running the Films workflow once by hand), what next.

Choices:

1. Run Films now, then measure and judge every audio track (recommended).
2. Run Films now, then stop.
3. Every audio track, without running Films now.
4. Stop here for now.

Answer: 1. (The Films workflow couldn't be started from this session: GitHub answered 404 for it, not yet registered
after the merge; it runs on its own on Monday night, or by hand from Actions.)

Question 2: how mediaqc reads the tracks after the first. FrameReader's AudioReader (the FFmpeg path) reads only the
best audio stream, and ManagedCodecs' Mp4Demuxer exposes one audio track.

Choices:

1. Add the stream choice to FrameReader (`AudioReaderOptions.Stream`, as ffmpeg's -map 0:a:N) and release it; files
   with several audio tracks go through the FFmpeg path (recommended).
2. Decode the other tracks in this repository through FFmpeg.AutoGen and feed FrameReader's managed meters.
3. Not now: keep judging the first track and naming it.

Answer: 1. bisforboman/shotdetector#697 adds `AudioReaderOptions.Stream`; media-qc waits for the FrameReader release
that carries it (the owner's to make, by that repository's release policy).

With the code (FrameReader 2.21.0): the FFmpeg path measures each audio track as it did the first, from its own
line-up (`TrackMeasures`, `Measurements.OtherTracks`); the profile judges each by the same rules, every audio finding
then naming its track (`Finding.Track`, "Track 2: ...") and comparing with that track's -map 0:a:N; an "audio"
finding describes each track. ManagedMeasurer.CanMeasure leaves a file with several audio tracks to the FFmpeg path.
