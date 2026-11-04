# The report's layout, after a UI/UX review

Asked 2026-11-04. The owner thought the screenshots looked a bit off, so an agent reviewed the three surfaces (the HTML
report, `noga serve`'s page and `noga-app`) from real renders: desktop and phone widths, light and dark, empty,
running and finished states. Its findings:

- **The report doesn't lead with the answer**: a small verdict pill, then every finding in measurement order, so on
  make-samples.sh's programme the one failure (loudness) was row 26 of 27, under the Info rows.
- **The report's table overflowed**: wider than the page at 1440 px, and 1443 px wide on a phone; "Checked against"
  took a third of the width and squeezed the findings; a row per shot; the format as a wrapped Info row; no print
  styles, though reports get printed to PDF for clients.
- **The three surfaces don't look like one product**: tinted badges in the report and the web UI, solid ones in the
  app; dark mode only in the app; "Failure", "Failed" and "Warnings" by surface; the CLI prints "Info" as a passing
  file's verdict.
- Job rows in the web UI and the app say "Failed" without why; web UI keyboard and feedback gaps; the app's dark-mode
  error colours below WCAG AA; "-Infinity dBTP" and ffprobe's "?" colour values shown raw.

## What next

- **Add the top five to the backlog, the report first** (recommended)
- **Fix the report now**, the rest to the backlog
- Fix all five now
- Keep the review only

**Answer: fix the report now, the rest to the backlog.**

## What was built

`ReportHtml`: a header with the file, profile, start timecode and the format (ffprobe's line, no longer a row); the
verdict as a block with a count per severity ("1 failure · 2 warnings · 6 passed · 14 for information"); the failures
and warnings in their own table, failures first; then the other checks in the profile's order; the shot list folded
in a `<details>` at the end under the shots summary. Five columns (result, check, finding, from, to) at fixed widths;
what a figure is checked against and its EBU QC item on a muted line under the finding (each figure still says how it
is checked against FFmpeg). Rows stack on a phone (no sideways scroll at 390 px), dark mode follows the system on
screen, and print keeps the colours, repeats the table head and doesn't split rows.
