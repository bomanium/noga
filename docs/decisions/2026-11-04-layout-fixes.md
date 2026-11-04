# Small layout fixes from the second review

Asked 2026-11-04, the last of the second UI/UX review's top five (decisions/2026-11-04-rows-in-place.md): which of
its small layout fixes to make.

- **All of them** (recommended): the web UI's empty "Checks" text squeezed into the badge column, and its path field
  refusing a file; the app's narrow-window buttons against the badge, its empty window mostly blank, and no way to
  pick a folder; both showing the start of a deep folder rather than its end; "Couldn't read" in the same grey as
  "Queued"
- The web UI's and the shared ones only
- Stop after this

**Answer: all of them.**

## What was built

- Web UI: the empty "Checks" line takes the list's width; a file's path in the path field (as pasted from a file
  manager) opens the folder it is in (`Folders.List`); the field's label says so.
- App: until the first check, the drop zone fills the window under the top row, its hint centred, and keeps to a line
  once there are checks; "Add folder…" beside "Add files…" (a folder's media files, subfolders too, as a drop);
  in a narrow window a row's buttons stay on the right of its badge.
- Both: a row's folder keeps its end ("…/season 2/ep01/masters", `Job.ShortFolder`, at most 48 characters), so deep
  folders don't all show the same start cut off; the whole path on hover.
- "Couldn't read" has a tone of its own (`unreadable`, a muted violet, WCAG AA light and dark like the others), unlike
  a queued check's grey, and still not a failure's red.
- The README's screenshot of the app is redone.
