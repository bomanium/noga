# What's next once the backlog's Next was empty

Asked 2026-11-06, after repeated frames (#64) and AS-11 parked (#65) left Next and Waiting empty.

Question: which Maybe item becomes the next one?

- **Meters into FrameReader** (recommended): switch Noga to the meters FrameReader now publishes
  (2026-11-05-meters-upstream.md) and delete Noga.Ffmpeg's copies; debt already decided on.
- The shots from the picture pass's frames: one decode less, but not for ProRes HQ, the main case.
- A macOS release: osx-arm64, signed and notarised.
- Tag a first release (v0.1.0) before more checks are added.

**Answer: meters into FrameReader.** FrameReader 2.24.0 (ChannelMeter, FrameSteps, InterlaceDetector, the loudest
momentary and short-term blocks and when, packet sizes and keyframes) and 2.25.0 (ConstantColourDetector,
GamutMeter) carry them, with FreezeDetector from before; 2.25.0's release waited on the owner's approval that day.
