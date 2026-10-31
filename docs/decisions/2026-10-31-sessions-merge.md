# Sessions merge their PRs

Overtakes [2026-10-31-merging-by-hand.md](2026-10-31-merging-by-hand.md), where the owner merged every PR by hand.

Question: none was put; the owner said it after PR 4: "For the future, just merge any branches. It's version
controlled so we can redo anything."

Answer: a session merges its PR itself (a merge commit, as the earlier PRs were) once CI is green on the PR's head.
A PR with a decision still open for the owner waits for the answer first. Nobody pushes to `main` directly.
