# Noga's meters into FrameReader

Asked 2026-11-05, when the owner asked whether the dropped-frame check could live in FrameReader or ShotDetector.
2026-10-31-where-the-measures-live.md puts the measures in the public packages and the verdicts here, but the
meters added since (FreezeMeter, InterlaceMeter, ChannelMeter, LoudnessBlocks, the gaps in VideoPackets.Times) and the
packet reader (Packets.Read, FFmpeg.AutoGen directly) were written in Noga.Ffmpeg. ShotDetector is about shots, so
not there.

Question: should Noga's measures move into FrameReader?

- **A backlog item, all at once** (recommended): one Maybe item to publish all of Noga's meters and a packet reader
  into FrameReader in one release, then switch Noga over. Nothing changes now.
- Just the packet reader: a Next item to add one to FrameReader (beside MediaProbe) and drop Packets.Read's unsafe
  code here; the meters stay.
- Leave as is: the meters stay in Noga.Ffmpeg; nothing on the backlog.

**Answer: a backlog item, all at once.** Noga keeps the verdicts, the profiles and the reports.
