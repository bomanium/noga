# Open films in CI, and joining range spans

Question 1: what next after the watch folder.

Choices:

1. Open films in CI (recommended): the measures on real material against ffmpeg, not only on generated files.
2. A shared-machine mode for `mediaqc serve`.
3. A native app shell around the page.
4. Stop here for now.

Answer: 1.

Question 2: which films, how often. A 10-minute 1080p film takes ~2 min to check and ~10 min more for the ffmpeg
comparisons; private Actions minutes count; none of Blender's films has AAC in MP4, so all go through the FFmpeg path.

Choices:

1. Big Buck Bunny 1080p (H.264/MP3 in MP4, 10.5 min, 276 MB) and Tears of Steel 720p (H.264/MP3 in MOV, 12 min,
   372 MB), pinned by SHA-256 and cached; weekly and on demand, ~30 min a run (recommended).
2. As 1, plus Big Buck Bunny remuxed with its audio as AAC, for the managed path; ~40 min a run.
3. Big Buck Bunny only, nightly.

Answer: 1.

Question 3: Big Buck Bunny's first check showed dozens of range warnings of one to three frames where frames hover
about 1% outside broadcast range. Join nearby spans?

Choices:

1. Spans less than 1 s apart join into one warning, first start to last end, with the worst frame's share
   (recommended).
2. Spans shorter than 0.5 s aren't reported.
3. Leave every span.

Answer: 1. `Profile.RangeMergeGap` (1 s for ebu-r128), `RangeSpan.Merge`.

Question 4: the first film run found Big Buck Bunny's loudness 0.38 LU off ffmpeg's: it has two audio tracks (MP3
stereo, then AC-3 5.1); mediaqc measures the first (-18.7 LUFS), ffmpeg on its own picks the one with the most
channels (-19.1 LUFS). Which track(s) should a report judge?

Choices:

1. The first track, and the report says which (recommended): an "audio" finding names it ("track 1 of 2: mp3, 2
   channels (stereo)") and that the others aren't judged; the comparisons map it (-map 0:a:0). Every track later
   (backlog).
2. Every track, each with its own findings.
3. ffmpeg's choice, the track with the most channels.

Answer: 1.

With the code: tools/get-films.sh downloads both into films/ (ignored by git) and checks their SHA-256;
tests/MediaQc.FilmTests compares each figure with the ffmpeg command line through the sample tests' FfmpegCli; it
runs only where MEDIAQC_FILMS names the folder (elsewhere a runsettings file filters every test out, so `dotnet test
MediaQc.slnx` doesn't download or run them); .github/workflows/films.yml runs it on Monday nights and on demand, the
films cached between runs.
