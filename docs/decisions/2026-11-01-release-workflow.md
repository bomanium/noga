# Release workflow

Question 1: what next after the shot list (every check the description names is in the report).

Choices:

1. A local web UI, `mediaqc serve` (recommended).
2. A release workflow: on a tag, CI publishes the Linux and Windows archives on a GitHub release.
3. Open films in CI.
4. Stop here for now.

Answer: 2.

Question 2: how the release is created when a tag like v0.1.0-alpha.1 is pushed (CI checks the tag is v and
Directory.Build.props' Version, builds, tests, runs tools/publish.sh and the Linux build on a sample).

Choices:

1. A draft release with the archives and generated notes, published by the owner on GitHub (recommended).
2. Published right away, a pre-release when the version has a suffix (-alpha.1).

Answer: 2.

With the code: ci.yml runs on `v*` tags too. The build job checks the tag first, then, after the usual steps, keeps
the archives (actions/upload-artifact); a release job, the only one with `contents: write`, publishes them with
`gh release create --generate-notes --verify-tag`. To release: set Version in Directory.Build.props in a PR, merge
it, then tag main's merge commit (`git tag v0.1.0-alpha.1 && git push origin v0.1.0-alpha.1`).
