# Switching to FrameReader's meters: a baseline tag, and meters that differ

Asked 2026-11-07, the morning FrameReader 2.25.0 and ShotDetector 3.30.0 were published to NuGet, which the backlog's
one Waiting item (2026-11-06-next-meters-upstream.md) waited on. An advisor's read suggested two questions first.

Question: tag a first release on main before the switch, as a fixed baseline to compare reports against?

- **Switch first, no tag** (recommended): the tests and the samples compare before and after; tag once it has landed.
- Tag v0.1.0 first: Version 0.1.0, a v0.1.0 tag on main and its GitHub release, then the switch.
- Tag an alpha first: v0.1.0-alpha.2 as a pre-release baseline, then the switch.

**Answer: switch first, no tag.**

Question: when one of FrameReader's meters doesn't give exactly the figures Noga's own does?

- **Accept it within FFmpeg's tolerance** (recommended): switch anyway, the difference recorded here.
- Fix it upstream, then wait: keep Noga's meter, fix FrameReader and switch that meter after its next release.
- Keep Noga's meter: switch the ones that match exactly, a backlog item for each of the others.

**Answer: fix it upstream, then wait.** Each meter is switched only when FrameReader's gives Noga's figures exactly;
one that doesn't stays in Noga.Ffmpeg until a FrameReader release fixes it.
