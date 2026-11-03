# MIT licence, public repository, a real name first

The owner's doubt: selling copies of media-qc makes it a middle man for FFmpeg's and the codecs' licences and patents
(H.264 and AAC pools, ProRes decoded by FFmpeg), on a small market that wants trust and support more than a tool.
The alternative raised: license it MIT and earn from support, integration and custom delivery profiles instead.

## Which parts go MIT?

- **All of it MIT** (recommended): core, CLI, web UI and desktop app; earn from support, pipeline integration,
  custom profiles and training. No licence split to maintain.
- Open-core: core and CLI MIT, the desktop app and extras closed and paid.
- Stay closed for now and revisit after v1.

Answer: **all of it MIT**.

## When does the repository go public?

- After a prep pass (recommended): history checked, LICENSE and README, the docs reworded, the name checked.
- After v1.
- **Right away.**

Answer: **right away**. The history was checked the same day: no media, keys, tokens or certificates, the only
personal address the owner's GitHub noreply one. Making it public is the owner's (repository settings).

## The working name

- **Pick a real name first** (recommended): renaming after people link to it is harder; check it is free on GitHub
  and NuGet and clashes with no trademark.
- Keep media-qc and rename later.

Answer: **pick a real name first**: the repository goes public once it has one (docs/backlog.md, Waiting).

## What changes

LICENSE (MIT) at the root, shipped in every release as `LICENSE.txt`; the README, CLAUDE.md, docs/dependencies.md and
the build files no longer call it proprietary. The dependency rule stays as it was (MIT, BSD, Apache-2.0; LGPL only as
separate, replaceable binaries; no GPL or AGPL), so a release stays usable by anyone, closed-source commercial use
included. `IsPackable` stays false: it is an application, not a library.
