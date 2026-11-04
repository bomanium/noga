# The app's drop zone and narrow window

Asked 2026-11-04, after the raw values were merged (decisions/2026-11-04-raw-values.md): the last of the UI review's
items.

- **The drop zone** (recommended): highlight it on drag-over, keep it from taking room once there are checks, and fix
  the hint and file names cut off in a narrow window
- Stop here

**Answer: the drop zone.**

## What was built

- Files can be dropped anywhere in the window, the list too; while they are dragged over it, the drop zone shows the
  accent border on the info tint (`MainWindow.Highlight`).
- Once there are checks, the drop zone keeps to its first line ("Drop video files or folders here") with less padding,
  leaving the room to the list.
- The hint lines wrap, centred, rather than being cut mid-word; file names end in an ellipsis.
- Below 600 px wide, a row's badge and buttons share a line and its text takes the width under them (they used to
  squeeze it a few words wide), redrawn when the window crosses that width.
- A row's time comes before its folder ("took 3 s · /home/user/deliveries"), in the app and the web UI alike, so a
  long folder cut off at the end no longer hides it.
- The README's screenshots of the app and the web UI are redone.
