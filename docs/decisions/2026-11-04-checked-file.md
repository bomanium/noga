# Which file a report is of, and when it was checked

Asked 2026-11-04, the second of the second UI/UX review's top five (decisions/2026-11-04-rows-in-place.md): the
report and the JSON said neither when the file was checked nor which file exactly, so a report passed on to a
broadcaster couldn't show which version of a delivery it covers.

## What identifies the file

- **The check's date, the full path, the size and the modified time** (recommended): costs nothing, and answers
  "which version did you check?" in practice
- Those and a checksum (MD5 or SHA-256 of the whole file): proof of the exact bytes, but a second read of the file,
  about a minute more for a 60-minute ProRes HQ master (~70 GB)
- Skip this item

**Answer: the check's date, the full path, the size and the modified time.**

## What was built

- `CheckedFile` (Noga.Core): the full path, the size in bytes and the modified time as the check finds the file before
  reading it, and when the check started; `QcReport.Source`, set by `Checker.Run` for the command line, the web UI
  and the app alike (null for a report made from measures alone, as in tests).
- The HTML report's header: "Checked 2026-11-04 14:32 +01:00 · Profile ebu-r128 · ...", then a line with the path,
  the size ("94.2 MB (94,201,579 bytes)") and "modified 2026-11-04 08:32 +01:00", times in the checking machine's
  zone with its offset.
- The JSON report: `path`, `size` (bytes), `modified` and `checked` (ISO 8601 with the offset) at the top level.
- The README's example report and its screenshot are redone.
