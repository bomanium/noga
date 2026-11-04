# A second UI/UX review, and job rows updated in place

Asked 2026-11-04, after every finding of the first review was fixed (decisions/2026-11-04-report-layout.md to
2026-11-04-app-drop-zone.md): a fresh agent reviewed the report, `noga serve` and `noga-app` again from new renders.
Its verdict: the three now look like one product (the verdict instant on each, the same labels, colours at WCAG AA
light and dark, nothing scrolling sideways). What it found left, by impact:

1. While a check measures, both job lists are rebuilt every second: in the web UI a focused Report link lost focus
   within 1.5 s and 10 clicks on Report opened 8 reports; the app the same, from its code.
2. The report and the JSON don't say when the file was checked, nor which file exactly (full path, size, modified).
3. Nothing shows a long check's progress: a 60-minute ProRes master shows "measuring N s" for about 17 minutes.
4. The report's format line is ffprobe's words ("mov,mp4,m4a,3gp,3g2,mj2", "yuv422p10le"); in print the timecodes
   overrun the To column, the shot list never prints, and a point (not a span) shows an empty To.
5. Small layout fixes: the web UI's empty "Checks" text squeezed into the badge column and its path field refusing a
   file; the app's narrow-window buttons against the badge, its empty window mostly blank, no "Add folder…"; both
   showing the start of a deep folder rather than its end; "Couldn't read" in the same grey as "Queued".

## What next

- Fix 1 now, the rest to the backlog (recommended)
- **Fix the top five in turn**, a PR each, asking before each
- Backlog only, back to speed on long files
- Stop here

**Answer: fix the top five in turn.**

## What was built (1)

- The web UI keeps each job's row and builds it again only when the job changes (its status, reason, error, start or
  end); each second only its time is written. The list is changed only where it must be: a new job inserted at the
  top, a changed one in its place. With a check measuring, a focused Report link kept its focus and 10 clicks opened
  10 reports.
- The app does the same (`MainWindow.Render`, rows kept by job and rebuilt when the job, the theme or the narrow
  layout changes); a test checks a finished check's row is the same control after another is queued.
