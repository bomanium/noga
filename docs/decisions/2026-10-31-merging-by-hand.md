# Merging by hand

The owner's other repositories turn auto-merge on for every PR, with `main` protected by required checks. On a
personal GitHub Free account, a private repository has neither: auto-merge and branch protection (or rulesets) need
GitHub Pro or Team.

Question: how media-qc's PRs get merged.

Choices:

1. The owner merges by hand once CI is green (recommended): no setup and no cost, and a parked project has few PRs.
2. A workflow that merges a PR labelled `automerge` once CI passes, with a write token: works on Free, but costs
   Actions minutes, and with `main` unprotected the CI gate is only a convention.
3. GitHub Pro, so auto-merge and `main`'s protection work as in the other repositories.

Answer: 1. Sessions open the PR and say when it is ready; nobody pushes to `main` directly, though settings don't
stop it. If the account moves to Pro, this is worth revisiting.

Since 2026-10-31 ([2026-10-31-sessions-merge.md](2026-10-31-sessions-merge.md)) sessions merge their own PRs once CI is
green.
