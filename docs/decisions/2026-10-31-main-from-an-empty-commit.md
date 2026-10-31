# main from an empty commit

The repository was created empty, so there was no `main` to open the scaffold's pull request against.

Choices:

1. Push the scaffold branch's empty root commit ("Initial commit") as `main`, then open the scaffold as a PR with
   auto-merge on (recommended).
2. The owner creates `main` by hand.
3. Push the whole scaffold straight to `main`, without a PR.

Answer: 1. `main` starts at the empty commit; everything else arrives through PRs.
