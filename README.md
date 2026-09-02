# gh-runner

Custom orchestration repository for building a patched GitHub Actions runner.

This repository keeps automation, patch files, and GitHub workflows only. It does
not vendor the `actions/runner` source tree into `master`.

## Goal

- Import the complete upstream Git history from `actions/runner` for a selected release branch.
- Create or update the matching local branch, for example `releases/m337`.
- Add one local orchestration commit that removes upstream workflow files at branch HEAD and restores this repository's automation.
- Apply local forward-port patches as separate commits on top of upstream history.
- Build the patched runner, publish the generated packages in a GitHub Release, and push a docker image to GHCR.

The resulting branch history is shaped like this:

```text
actions/runner history -> upstream release HEAD -> local orchestration overlay -> patch commits
```

## Repository Layout

- `.github/workflows/sync-upstream.yml` imports a selected upstream release branch and dispatches the release build workflow.
- `.github/workflows/watch-upstream-releases.yml` runs on a schedule, checks the latest upstream runner release, imports it when needed, and dispatches a release build.
- `.github/workflows/build-runner.yml` is dispatch-only; it builds packages, publishes them to a GitHub Release, and pushes a GHCR docker image named after this repository, such as `ghcr.io/rhscz/gh-runner`.
- `.github/workflows/build-runner-test.yml` runs on push or manual dispatch and uploads package plus docker image artifacts without creating a GitHub Release or pushing to GHCR.
- `scripts/import-upstream-history.sh` creates the local branch from full upstream history, overlays local automation, and commits patches.
- `scripts/apply-patches.sh` applies the patch series with limited fuzz so small upstream context shifts can be tolerated.
- `scripts/build-runner.sh` wraps the upstream runner build and package commands.
- `scripts/sync-upstream.sh` is a local helper for creating an ignored working snapshot under `.work/actions-runner`.
- `patches/` contains the custom patch series in deterministic order.

## Patch Flow

Patch order is controlled by `patches/series.txt`. The patcher uses
`patch --forward --fuzz=3`; this lets it survive small context movement while
still failing when a hunk cannot be applied.

## Release Branches

The orchestration branch is expected to be `master`. Imported runner branches can
use the same names as upstream, such as `releases/m337`.

GitHub Release tags use the same format as upstream `actions/runner`, such as
`v2.337.0`.

Upstream workflow files may exist in older imported history because they are part
of the original `actions/runner` commits. They are removed again at the imported
branch HEAD by the local overlay commit.
