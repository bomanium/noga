# Per-channel audio faults

Asked 2026-11-03, after the delivery format, when the owner chose per-channel audio faults next (the 2026-11-03
review's second pick: decisions/2026-11-03-delivery-format.md). Until then a silence had to be on every channel at
once, so a file with a dead channel passed.

## How each fault counts

- **A dead channel fails, the rest warn** (recommended): a failure for a channel silent for the whole programme while
  another has sound; a warning for a channel silent for 2 s or more while the others have sound, for a stereo pair
  mostly out of phase, and for clipped runs; information for dual mono (left and right the same), which can be
  intended.
- All warnings (dual mono information).
- All failures, dual mono included.

**Answer: a dead channel fails, the rest warn.**

## The thresholds

- **Phase below -0.3, 3 samples at full scale** (recommended): out of phase when the pair's mean phase correlation is
  below -0.3 (FFmpeg's aphasemeter: +1 in phase, 0 unrelated, -1 inverted); clipping at 3 or more consecutive samples
  at full scale on a channel (astats per channel); dual mono when the correlation is above 0.999 and the levels are
  within 0.1 dB.
- Stricter: below 0, 2 samples.
- Looser: below -0.5, 5 samples.

**Answer: below -0.3, and 3 samples.** Profile files can change both (`minPhase`, `clipRun`).

## What was built

- `ChannelMeter` (MediaQc.Ffmpeg), fed the decoded samples on both paths (FrameReader's AudioReader, ManagedCodecs'
  AAC): each channel's silences (FrameReader's SilenceFinder on that channel alone), its RMS level, its samples at full
  scale (32767/32768 and up, a 16-bit sample's largest) and its runs of `clipRun` or more there, and the first two
  channels' phase as aphasemeter computes it (the mean over the samples of 2·L·R / (L² + R²), 1 where both are 0, so
  silence counts as in phase). Every audio track has its own (`Measurements.Channels`, `TrackMeasures.Channels`).
- Checked against the ffmpeg command line (`FfmpegCli.AssertChannels`), on generated stereo files and make-samples.sh's
  programmes (both paths for the MP4): each channel's silences are `pan=mono|c0=cN,silencedetect`'s, each end within
  10 ms; its RMS level astats' within 0.1 dB; the phase the mean of `aphasemeter` over 4800-sample frames within 0.01;
  and a channel that reaches full scale up and down has exactly astats' Peak count of samples there (astats counts
  samples at the highest and lowest level, not runs, so runs aren't a figure FFmpeg prints).
- The findings (`channel.*`, every track's, each naming its track when there are several):
  - `channel.dead`: failure. A channel silent from the first to the last sample while another has sound; when every
    channel is silent, that is the track's own silence (and loudness), not a channel's.
  - `channel.silence`: warning. A channel's silence of `minSilence` or more, without the parts where every channel is
    silent (those are `silence`).
  - `channel.phase`: pass, or a warning below `minPhase` (the first two channels: a stereo pair, or a 5.1's front left
    and right); left out when either is dead.
  - `channel.dual-mono`: information, when the pair has sound.
  - `channel.clipping`: a warning per channel that clips, else one pass.
- EBU QC items: silence 0078B (any channel), phase 0012B, clipping 0005B, mono 0124B.
