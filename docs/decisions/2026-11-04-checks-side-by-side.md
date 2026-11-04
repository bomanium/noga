# Checks side by side

**Question** (2026-11-04): the backlog's next item was the speed on long files (the open films benchmarked, the
figure in the README). Take it, and also the Maybe item to run 2 checks at a time in `Jobs`?

**Choices**

1. The films benchmark only (recommended at the time: the backlog's Next item, nothing else).
2. The films benchmark, then 2 checks at a time in `Jobs`: docs/notes/performance.md found that a check keeps about
   2 of 4 cores busy, and that 2 at a time (separate processes) finished a queue 1.6x sooner.
3. Not now.

**Answer**: 2, both.

**What that means**

- `Jobs` (the web UI, its watch folder, the app) runs `Jobs.DefaultAtATime` checks at once: 2 on 4 cores or more,
  else 1. Four at a time gained little over two (1.8x against 1.6x) and takes twice the memory (about 200-300 MB a
  check), so it stays at 2. The checks start in the order they were asked for. The constructor takes another count,
  and there is no option for it yet: one can be added if someone asks.
- The `noga check` command checks one file, so it doesn't change.
- Checks side by side run in the same process, which the 1.6x was not measured on. A test
  (`SampleTests.ChecksSideBySideGiveTheSameReports`) checks make-samples.sh's three programmes all at once, on both
  decoding paths, and gets the same reports as one at a time. docs/notes/performance.md has the figures in one
  process.
