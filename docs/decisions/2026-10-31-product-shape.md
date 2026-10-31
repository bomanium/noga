# Product shape

Question: the shape of v1.

Choices:

1. A CLI first, writing an HTML report file, with a JSON report and an exit code for pipelines (recommended); a UI
   later on the same core.
2. A local web UI: `mediaqc serve` opens a page in the browser to drop files on; the CLI too.
3. A desktop app (e.g. Avalonia): the most work, packaged per OS.

Answer: 1: `mediaqc check <file>` writes an HTML and a JSON report and exits with a code for pipelines. Asked
twice the same day: the first answer was 2 (a local web UI), revised to 1 before anything was built on it. A local
web UI stays a later option on the same core.
