# One status style for the report, the web UI and the app

Asked 2026-11-04, after the report's new layout was merged (decisions/2026-11-04-report-layout.md): which of the UI
review's backlog items next.

- **One status style** (recommended): one set of status colours and labels, light and dark, in Noga.Core, used by
  the report, the web UI and the app, so they look like one product; it also fixes the app's dark-mode badges, which
  were below WCAG AA
- Job rows that explain themselves in the web UI and the app
- Stop here

**Answer: one status style.**

## What was built

- `StatusStyle` (Noga.Core): a file's `Status` (queued, measuring, passed, passed with warnings, failed, couldn't
  read) with one label each, used everywhere: "Passed with warnings" (it was "Warnings" in the web UI and the app),
  and the command line's first line says "Failed" or "Passed" rather than the verdict's severity ("Failure", or
  "Info" for a file that passed). The JSON report's `verdict` and the exit codes are unchanged.
- Five tones (pass, info, warning, failure, neutral), each a tinted background with coloured text, light and dark;
  "Couldn't read" is neutral, as it has no verdict. `StatusStyle.Css` gives a page each tone's custom properties
  (dark ones on screen with a dark system theme) and a class per tone; a test checks every tone's text against its
  tint is at least 4.5:1 (WCAG AA), light and dark.
- The report takes its badge colours from it; the web UI too, with the page itself now following the system's dark
  theme, its badges in one aligned column (stacked on a phone), and each job's `status` in its JSON; the app draws
  tinted badges in the colours of its theme (light or dark, redrawn when it changes), its error text the failure
  tone's.
- The README's screenshots of the web UI and the app are redone.
