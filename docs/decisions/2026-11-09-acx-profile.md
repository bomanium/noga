# An acx profile: audiobooks, judged by RMS and sample peak

Asked 2026-11-09, when issue #78 (audiobook and podcast profiles) was taken up. Noga already checks a sound-only
file, but spoken-word deliveries are judged by other figures than broadcast loudness: ACX (Audible's Audiobook
Creation Exchange) asks for an RMS window, a peak ceiling, a noise floor and an MP3 format.

## ACX's figures

ACX's audio submission requirements (help.acx.com) and its blog couldn't be fetched from the session that built this
(the hosts are outside its network); the figures are as search results quoting those pages give them, the same in
each, and as the issue listed them:

- each file between -23 and -18 dB RMS
- peaks no higher than -3 dB
- a noise floor no higher than -60 dB RMS
- MP3 at 192 kb/s or more, constant bit rate (CBR), 44.1 kHz
- each file no longer than 120 minutes
- 1 to 5 s of room tone at the head and tail (ACX's blog: 0.5 to 1 s at the head); every file mono or every file
  stereo, not joint stereo; opening and closing credits in files of their own

Read the page again when it can be reached, and change the profile if a figure moved.

## Is batch checking, a command line and a report enough of a gap?

Free checkers for one file at a time exist (ACX's own Audio Lab, Audacity's ACX Check).

- **Yes: the ACX slice now** (recommended): the single-file acx profile now (RMS, peak, format; the noise floor once
  FrameReader has its meter), the set check a later backlog item.
- Yes, with the set check: only worth it with a whole book as one check; both in this round.
- No, park it: a Maybe in the backlog, nothing built.

**Answer: yes, the ACX slice now.**

## Which services' specifications first?

- **ACX only for now** (recommended): ACX's is the published, widely used spec; other audiobook services and podcast
  targets as later profiles when someone asks.
- ACX and other audiobook services (Findaway/Spotify, Kobo, Google Play Books), each page read first.
- ACX and podcast targets (Apple's -16 LUFS integrated and the like), mostly integrated loudness Noga measures.

**Answer: ACX only for now.**

## Sample peak or true peak?

ACX says peaks no higher than "-3 dB", not dBTP, and doesn't say which peak.

- **Sample peak fails** (recommended): above -3 dBFS fails (astats' Peak level, as ACX's dB and most DAW meters read
  it); true peak shown beside it as information.
- True peak fails: above -3 dBTP (ebur128's, already measured): stricter, passes either reading, but can fail files
  ACX accepts.
- Sample fails, true warns.

**Answer: sample peak fails.** The sample peak is FrameReader's LoudnessMeter's (ebur128's sample peak), kept on
`LoudnessMeasure.SamplePeak` on both decoding paths, from the end of any line-up as loudness is; `level.peak` judges
it against `Profile.MaxSamplePeak`, checked against astats' Overall Peak level within 0.01 dB. The true peak stays
`loudness.true-peak`, information under acx (`Profile.MaxTruePeak` null).

## A WAV master under acx?

ACX takes MP3 only.

- **Two profiles** (recommended): `acx` fails anything but an MP3 at 192 kb/s or more, CBR, 44.1 kHz; `acx-master`
  checks a WAV's levels and sample rate without the MP3's rules.
- One profile that fails a WAV on format; its levels still there to read.
- One profile, codec-aware: bit rate and CBR only for an MP3, a WAV passes on format.

**Answer: two profiles.**

## How it is built

- `Profiles.Acx`: from ebu-r128, with no loudness target and no true-peak limit (`TargetLoudness` and `MaxTruePeak`
  are now optional: null reports the figure, information, without judging it), no line-up tone level,
  `MinRms` -23, `MaxRms` -18, `MaxSamplePeak` -3, and a format: container and codec mp3, 44100 Hz,
  `MinAudioBitRate` 192000, `ConstantBitRate`, at most 7200 s. `Profiles.AcxMaster`: the same levels, the format
  44100 Hz and at most 7200 s. Both are among `Profiles.All`, so `--profile acx`, the web UI and the app list them; a
  profile file can start from either (`basedOn`) and set `minRms`, `maxRms`, `maxSamplePeak`, and in its format
  `minAudioBitRate` and `constantBitRate`.
- `level.rms`: the track's RMS level, every channel's samples together over the whole track: astats' Overall RMS
  level, computed from FrameReader's ChannelMeter's per-channel RMS (each channel has as many samples, so the overall
  mean square is the mean of theirs; `ChannelMeasures.OverallRms`), checked within 0.05 dB. Judged at the report's
  precision (0.1 dB), a failure outside the window; digital silence fails. Reported only where a profile sets the
  window, as other profiles have no use for it.
- `format.audio-bit-rate`: ffprobe's bit_rate (`AudioFormat.BitRate`), at least the profile's; a VBR MP3's is its
  average.
- `format.bit-rate-mode`: ffprobe doesn't say CBR or VBR, so Noga.Ffmpeg's `Mp3Headers` reads an MP3's frame headers
  (Layer III, MPEG-1, 2 and 2.5; after the ID3v2 tag; the Xing, Info or VBRI frame left out, as FFmpeg's mp3 demuxer
  does) for a file whose container is mp3 (`Measurements.Mp3`): constant when every frame has the same bit rate. It is
  a header reader, as SoundHeaders is, not a meter, so it lives in Noga. Checked against `ffprobe -show_packets` (one
  packet a frame): the count, the bytes and each frame's bit rate from its size, exactly. PCM is constant by nature;
  any other codec's mode isn't known and fails.
- The noise floor waits on FrameReader: [2026-11-09-noise-floor.md](2026-11-09-noise-floor.md). Room tone at the
  head and tail, mono or stereo across a book, and the set check: backlog.
