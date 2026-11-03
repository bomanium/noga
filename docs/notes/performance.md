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

## Where the time goes

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

## A queue of files

8 checks of programme_prores.mov (20 s each), N at a time in separate processes (`xargs -P`), as `Jobs` would run
them if it ran more than one:

| At a time | Wall | Wall a file | Against one at a time | Peak memory (one process) |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 54.8 s | 6.8 s | 1.00x | 192 MB |
| 2 | 33.4 s | 4.2 s | 1.64x | 186 MB |
| 4 | 30.6 s | 3.8 s | 1.79x | 191 MB |

Two at a time finishes a queue about 1.6x sooner on 4 cores, and four at a time adds little more. `Jobs` (the web UI,
the watch folder, the app) runs one at a time, so half the machine sits idle during a batch. Memory is per check,
about 200-300 MB each.

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

## Not measured yet

- Real masters, 30-60 minutes, and UHD files: these figures are lavfi programmes, looped.
- Files on a network share. The web UI and the watch folder are made for those, and reading them may cost more than
  the decoding does.
- Windows, and the linux-arm64 build on real ARM hardware.
- Running several checks in one process, as `Jobs` would if it ran more than one at a time. The queue above runs
  separate processes.
