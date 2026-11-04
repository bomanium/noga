# The web UI's usability gaps

Asked 2026-11-04, after the job rows were merged (decisions/2026-11-04-job-rows.md): which of the UI review's three
remaining backlog items next.

- **The web UI's gaps** (recommended): folders reachable by keyboard, a bad path clears the old listing, feedback on
  Check and no duplicate checks, no Check on non-media files, "check all in this folder"
- Raw values in findings ("-Infinity dBTP", "?/?/bt709")
- The app's drop zone and narrow window
- Stop here

**Answer: the web UI's gaps.**

## What was built

- Folders and ".." are buttons, so Tab reaches them and Enter opens them; buttons, links and the path field show a
  focus ring.
- A path that can't be opened clears the listing under it (it read as that path's empty folder) and says why, the
  message announced to screen readers (`role="alert"`).
- Only media files have a Check button. It says "Queued" or "Measuring…" (the shared labels) and is disabled while the
  file's check waits or runs, from the moment it is pressed.
- `Jobs.Add` returns the job already queued or measuring for the same file rather than adding another, so a double
  press, "Check all" over a file already queued, or the watched folder can't check a file twice at once (the web UI
  and the app alike); once done, it can be checked again.
- "Check all N media files here" above a listing with two or more.
- The README's screenshot of the web UI is redone.
