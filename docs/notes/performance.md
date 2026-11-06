# Performance

How fast a check is, how much memory it takes, and how a queue of files goes, measured by `tools/benchmark.sh`
(decisions/2026-11-03-benchmark.md). Rerun it after a change that could move these figures, and update this note.

## The machine and the method

Noga 0.1.0-alpha.2 (3254b80 plus this note's fix), 2026-11-03: a cloud container with 4 CPUs (Intel Xeon @ 2.10GHz)
and 15 GB of memory, on Linux. The command is built in Release, and each check runs in its own process
(`noga check FILE --out FOLDER`): wall and CPU seconds, peak resident memory. The files are make-samples.sh's, plus
its 20 s ProRes and H.264 programmes looped to 10 minutes (`ffmpeg -stream_loop`, copied). So a long file does the
same work every second as the short one. These are one run each, and the figures moved by 10-20% between runs.

## One file

| File | Length | Wall | CPU | Cores busy | Wall a programme minute | Peak memory |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| programme_prores.mov (ProRes 422 HQ, PCM) | 20 s | 7.4 s | 14.1 s | 1.9 | 22.2 s | 186 MB |
| programme_dnxhr.mxf (DNxHR SQ, PCM) | 20 s | 3.8 s | 9.2 s | 2.4 | 11.4 s | 170 MB |
| programme_h264.mp4 (H.264, AAC: the managed path) | 20 s | 5.0 s | 11.5 s | 2.3 | 15.0 s | 279 MB |
| out_of_range.mov | 5 s | 2.4 s | 4.6 s | 1.9 | 28.8 s | 180 MB |
| letterbox.mov | 5 s | 2.6 s | 5.3 s | 2.0 | 31.2 s | 183 MB |
| programme_10min_h264.mp4 | 600 s | 77.0 s | 184.2 s | 2.4 | 7.7 s | 306 MB |
| programme_10min_prores.mov | 600 s | 172.4 s | 364.3 s | 2.1 | 17.2 s | 315 MB |

- A check takes about 2 s to start, so the short files cost more a minute than the long ones.
- 1080p25 ProRes HQ takes about 0.29x its length. A 1-hour master would be about 17 minutes on this machine, H.264
  about 8. The ProRes is 10-bit 4:2:2, and the DNxHR is 8-bit, which is why it is quicker.
- A check keeps about 2 of the 4 cores busy, on either path.
- Memory stays flat with length: 315 MB for 10 minutes of ProRes.

## The open films, and a second run (2026-11-04)

Noga 0.1.0-alpha.2 (a230fec), 2026-11-04: another cloud container with 4 CPUs (Intel Xeon @ 2.80GHz) and 15 GB.
`tools/benchmark.sh --films films`, the films from `tools/get-films.sh`. This container was slower than the first:
the same 10-minute ProRes took 245 s rather than 172 s. So compare the rows of a run with each other, not across runs.

| File | Length | Wall | CPU | Cores busy | Wall a programme minute | Peak memory |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| programme_prores.mov | 20 s | 9.2 s | 17.9 s | 1.9 | 27.6 s | 186 MB |
| programme_dnxhr.mxf | 20 s | 4.5 s | 10.6 s | 2.4 | 13.5 s | 170 MB |
| programme_h264.mp4 | 20 s | 7.0 s | 13.4 s | 1.9 | 21.0 s | 164 MB |
| programme_10min_h264.mp4 | 600 s | 122.3 s | 264.4 s | 2.2 | 12.2 s | 192 MB |
| programme_10min_prores.mov | 600 s | 245.2 s | 507.3 s | 2.1 | 24.5 s | 227 MB |
| Big Buck Bunny (1080p30 H.264, MP3 and AC-3 5.1, MP4) | 635 s | 146.8 s | 379.5 s | 2.6 | 13.9 s | 255 MB |
| Tears of Steel (1280x534 24 fps H.264, MP3, MOV) | 734 s | 59.2 s | 160.6 s | 2.7 | 4.8 s | 154 MB |

- Both films go to the FFmpeg path: Big Buck Bunny has two audio tracks, Tears of Steel is a MOV, and both have MP3.
- Real footage costs about what the looped programmes do: 1080p H.264 about 14 s a minute, and 720p about a third of
  that. Memory stays flat, at 255 MB at most for 10.5 minutes with two audio tracks.
- So nothing on the films needed fixing. A 1-hour ProRes HQ master takes about 17-25 minutes on 4 cores, and
  1080p H.264 about 15.



A 3-minute ProRes programme (the 20 s one looped) on the FFmpeg path, each stage timed on its own (50 s in all):

| Stage | Seconds | Share |
| --- | ---: | ---: |
| Decoding the video (FFmpeg, multi-threaded: 24 s of CPU) | 7 | 15% |
| PictureMeter (black, bars, BRNG, crop, picture types), on one thread | 20 | 40% |
| FreezeMeter and InterlaceMeter | 12 | 24% |
| Shots (ShotDetector decodes the video a second time) | 10 | 20% |
| Packets, probing, every audio pass (silence, tone, loudness, channels) | 1 | 2% |

The picture pass is serial: each frame is decoded, then measured, before the next one is decoded. The shots come from
a second pass over the file. Audio costs almost nothing.

## Faster single checks (2026-11-05)

What the table above showed, done (decisions/2026-11-05-faster-single-checks.md):

- The meters each take the frames on a thread of their own while the next frames are decoded (`FrameMeters`).
  PictureMeter runs on two threads, each frame measured on its own and the measures joined.
- On the FFmpeg path the shot list (on its own decode) and the sound run at the same time as the picture pass
  (`SideBySide`). On the managed path, which already detects the shots on the picture pass's frames, the sound does.

The same container, each file checked by the build before (main, 3c480a9) and after, one after the other.
The reports are the same:

| File | Before | After | Faster |
| --- | ---: | ---: | ---: |
| programme_prores.mov looped to 3 min (ProRes HQ) | 75.3 s | 40.4 s | 1.86x |
| programme_10min_prores.mov (ProRes HQ, two runs each) | 246 s, 234 s | 132 s, 131 s | 1.83x |
| programme_dnxhr.mxf looped to 3 min (DNxHR SQ) | 33.4 s | 23.4 s | 1.43x |
| programme_h264.mp4 looped to 3 min (the managed path) | 35.3 s | 27.0 s | 1.31x |
| Big Buck Bunny (FFmpeg path) | 138.0 s | 101.2 s | 1.36x |
| Tears of Steel (FFmpeg path) | 57.7 s | 49.0 s | 1.18x |

- 1080p25 ProRes HQ now takes about 13 s a minute on 4 cores, so a 1-hour master takes about 13 minutes rather than
  23. 1080p H.264 takes about 9-10 s a minute.
- A check now keeps 3 to 3.6 of the 4 cores busy. The ProRes uses about 140 s of CPU for 3 minutes (the picture pass
  92, the shots 47), so 35 s is the floor on 4 cores, against 40 s. A further gain has to do less work, not more at
  once: the shots' own decode is the largest piece left.
- Peak memory rose by about 50-100 MB (frames waiting for the meters): 310 MB for 10 minutes of ProRes, 358 MB for
  Big Buck Bunny.
- So two checks at a time gain less than before: 8 checks of the 20 s ProRes took 47.2 s one at a time and 38.5 s
  two at a time (1.23x, from 1.73x). Two at a time stays: it still finishes a queue sooner, and a check that
  can't use the cores (short files, their start-up) leaves them to the other.

The same benchmark run measured the 10-minute ProRes at 220 s, an outlier: checked again straight after, it took
132 s. In a shared cloud container a single run can be off by that much, which is why the table above compares
the two builds one after the other.

## A queue of files

8 checks of programme_prores.mov (20 s each), N at a time in separate processes (`xargs -P`), as `Jobs` would run
them if it ran more than one:

| At a time | Wall | Wall a file | Against one at a time | Peak memory (one process) |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 54.8 s | 6.8 s | 1.00x | 192 MB |
| 2 | 33.4 s | 4.2 s | 1.64x | 186 MB |
| 4 | 30.6 s | 3.8 s | 1.79x | 191 MB |

Two at a time finishes a queue about 1.6x sooner on 4 cores, and four at a time adds little more. Memory is per check,
about 200-300 MB each.

So `Jobs` (the web UI, the watch folder, the app) runs two at a time on 4 cores or more
(decisions/2026-11-04-checks-side-by-side.md). The same queue on 2026-11-04's machine, in separate processes and then
in one process through `Jobs` (a Release build; the peak memory is the one process's, all its checks together):

| At a time | Separate processes | Against one | One process, `Jobs` | Against one | Peak memory (`Jobs`) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 77.0 s | 1.00x | 74.0 s | 1.00x | 394 MB |
| 2 | 44.6 s | 1.73x | 42.1 s | 1.76x | 560 MB |
| 4 | 35.0 s | 2.20x | 36.1 s | 2.05x | 795 MB |

Checks side by side in one process gain as much as in separate processes. Two at a time takes about 170 MB more than
one.

## What the first run found: memory that grew with length

On the first run, the 10-minute ProRes took 1.9 GB and 221 s. `Checker.Run` first asks
`ManagedMeasurer.CanMeasure` whether the managed path reads the file. That opened every MP4 or QuickTime file with
ManagedCodecs' demuxer for all its tracks. A QuickTime file's PCM track has one sample per audio sample, so the
demuxer built a sample table of 48,000 entries a second (29 bytes each, written twice), only for the answer to be
no. That came to 478 MB for 3 minutes and 1.6 GB for 10. A 1-hour ProRes master would have taken some 10 GB before
its check started.

`CanMeasure` now reads only the video track's headers, and the audio codecs from FFmpeg's probe (the demuxer's audio
track only when FFmpeg's libraries don't load). That takes 1 MB for the same file. A test keeps it under 4 MB on
make-samples.sh's ProRes (`SampleTests.AskingTheManagedPathDoesNotReadAPcmTrack`).

## Constant colour (2026-11-06)

`ColourMeter` (decisions/2026-11-06-constant-colour.md) counts a histogram of every plane's samples, on a thread of its
own, like the freeze and interlace meters. On every frame that came to 30-55% more time for a check of make-samples.sh's
10-bit programmes. So it first counts every fourth row of the luma only. When those rows prove the frame's YHIGH and
YLOW are further apart than the spread, which is exact, the rest isn't counted. A moving picture stops there; a flat
one, black included, gets the full count, in four interleaved histograms so that runs of one value don't wait on each
other. On the 20 s programmes (14 s of bars and black), Release, 4 cores: DNxHR 3.1 to 3.5 s, H.264 4.2 to 4.7 s,
ProRes 4.4 to 4.6 s.

## RGB gamut (2026-11-06)

`GamutMeter` (decisions/2026-11-06-rgb-gamut.md) counts every pixel, so it can't stop early as the colour meter does.
A table of each chroma pair's run of Y codes in gamut makes a pixel one lookup and a comparison, the pair looked up once
for the two luma samples sited on it. The table is 1 M entries at 10 bits. Building it took 0.9 s while the search for
a run's edges walked the whole estimate when it was empty; the estimate is off by one code at most, and now the build
takes about 50 ms. With both of 2026-11-06's meters, a check of the 20 s programmes takes 2.9 to 3.5 s (DNxHR), 3.9 to
4.4 s (H.264) and 3.9 to 4.6 s (ProRes), Release, 4 cores: some 15-20%.

The repeated frames' meter (decisions/2026-11-06-repeated-frames.md) is a second FreezeMeter on a lane of its own.
In moving picture it stops each frame's sum within a few rows, as the freeze meter does, and copies the frame as its
next reference. On the 20 s programmes its cost is within the run-to-run noise: best of three, Release, 4 cores,
ProRes 6.31 → 6.35 s, DNxHR 4.49 → 4.72 s, H.264 5.90 → 5.80 s.

## Not measured yet

- Real masters, 30-60 minutes, and UHD files: these figures are lavfi programmes, looped, and the open films
  (10-12 minutes, H.264).
- Files on a network share. The web UI and the watch folder are made for those, and reading them may cost more than
  the decoding does.
- Windows, and the linux-arm64 build on real ARM hardware.
