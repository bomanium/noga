# Backlog

What media-qc does next, in priority order. Three sections: **Next** (open work, the first item first), **Waiting**
(items that wait on the owner or an outside event) and **Maybe** (ideas not committed to). Update a status when work
starts or lands; add an item when a gap turns up, with why it matters. An item moves to
[backlog-done.md](backlog-done.md) when it is Done, Dropped or Won't do. Design questions go to [decisions/](decisions/).

Status: **Open**, **In progress**, **Waiting**, **Maybe**; finished (in backlog-done.md): **Done**, **Dropped**,
**Won't do**.

## Next

Nothing: the report has every check the description names, a release workflow, a local web UI with a watch folder, and weekly checks on open films. What comes next
is the owner's to choose (Maybe below).

## Waiting

- The desktop app's icon: the chosen drawing in place of the provisional one
  - Why: the window icon, the Windows executable's icon, tools/make-icons.sh and the Linux desktop entry are wired
    (decisions/2026-11-02-app-icon-wiring.md) on branch claude/app-icon-desktop, its PR not merged until the icon is
    decided; a separate session researches concepts and asks the owner (decisions/2026-11-02-app-icon.md)
  - Status: Waiting (on the owner's choice of icon)

## Maybe

- The desktop app's next steps: installers (MSI or MSIX on Windows, an AppImage on Linux), macOS (osx-arm64, signed
  and notarised), code signing on Windows
  - Why: decisions/2026-11-01-desktop-app.md shipped mediaqc-app in the release folders, unsigned; the icon and a Linux
    desktop entry are under way (Waiting above, decisions/2026-11-02-app-icon-wiring.md)
  - Status: Maybe

- The web UI's next steps: HTTPS for a shared server (a given or self-signed certificate)
  - Why: decisions/2026-11-01-web-ui.md chose picking on the server and this machine only for a start; the watch folder
    (decisions/2026-11-01-watch-folder.md), the shared-machine mode (decisions/2026-11-01-shared-machine.md, plain
    HTTP for trusted networks) and a desktop app (decisions/2026-11-01-desktop-app.md) are done
  - Status: Maybe

- macOS (osx-arm64) and Linux arm64 releases
  - Why: FrameReader.Native has osx-arm64 and linux-arm64 packages; CI can't run them (ubuntu only, macOS minutes cost
    10x), and macOS wants a signed, notarised binary
  - Status: Maybe

- Code signing for the Windows build
  - Why: an unsigned mediaqc.exe from a download is blocked or warned about by SmartScreen
  - Status: Maybe

- The AVC patent pool before selling
  - Why: Software with an H.264 decoder falls under the Via LA AVC pool (the first 100,000 units a year carry no
    royalty, then $0.20 a unit). Check the current terms, and whether decoding through FFmpeg's libraries changes it,
    before the first sale (docs/notes/origin.md)
  - Status: Maybe

- Captions and ad markers
  - Why: Out of scope at first (docs/notes/origin.md); what customers ask for after the first version decides which
  - Status: Maybe

- OP-59's and ARIB TR-B32's figures checked against the originals
  - Why: decisions/2026-11-01-profiles.md took them from vendor summaries (ARIB's tolerance not found, ±1 LU assumed);
    a customer relying on op-59 or arib-tr-b32 needs them confirmed
  - Status: Maybe
