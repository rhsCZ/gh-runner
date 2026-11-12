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

## Importing, Patching, And Rebuilding A Release

Run **Watch Upstream Runner Releases** manually to import an upstream release,
apply the local patch series, and build it. Set `release_tag` to the upstream
tag (for example `v2.338.2`); `target_branch` is optional and defaults to the
matching `releases/m338` branch. The scheduled run performs the same check for
the latest upstream release.

If that patched release already exists in this repository, enable the manual
`rebuild_existing_release` option in the watcher. This forces a fresh import of
the upstream branch and applies the current patch series even when the
previously imported upstream commit has not changed. The workflow then replaces
release assets and versioned image tags. It leaves the Docker `latest` tag
unchanged. The existing Git tag itself does not move to a different commit.

**Build Runner** only builds a branch that already contains the patched runner;
it does not fetch upstream source or apply patches. Run it manually when you need
to rebuild an already prepared branch. Set `source_ref` to the branch, tag, or
commit to build. If its GitHub Release already exists, enable
`rebuild_existing_release`. `update_latest_on_rebuild` additionally moves the
Docker `latest` tag and is available only in this direct build workflow.

## Multi-Repository Runner

The multi-repo patch stores each repository registration as an isolated profile
under `.runner.d/<profile-name>` in the runner installation directory. Register
each repository once, using a unique profile name and that repository's
short-lived registration token from **Settings → Actions → Runners → New
self-hosted runner**:

```bash
cd /srv/github-runner
./config.sh add --profile synapse --url https://github.com/OWNER/synapse --token REGISTRATION_TOKEN --name shared-runner --labels self-hosted,linux
./config.sh add --profile api --url https://github.com/OWNER/api --token REGISTRATION_TOKEN --name shared-runner --labels self-hosted,linux
./config.sh list
```

Use a separate registration token for each repository. The profile name is a
local identifier; `--url` selects the GitHub repository. The runner discovers
profiles from `.runner.d` at startup, so restart `./run.sh` or its service after
changing registrations.

To remove a profile and its GitHub runner registration, provide a removal token
generated from that repository's runner settings:

```bash
./config.sh remove --profile synapse --token REMOVE_TOKEN
```

To delete only the local profile while leaving the GitHub registration in place,
use `--local`:

```bash
./config.sh remove --profile synapse --local
```

`./config.sh list` shows configured profile names, runner names, and repository
URLs. The ordinary interactive `./config.sh` flow configures a single root
runner; for multi-repo registrations, use `config.sh add` with `--profile`,
`--url`, and `--token` as above.

Set the shared maximum parallel job count in the runner installation directory:

```bash
./config.sh set --maxconcurrentjobs 2
```

The default is `1`. The CLI value takes precedence over
`ACTIONS_RUNNER_INPUT_MAXCONCURRENTJOBS`, then the shared `.options` value. The
effective parallelism is also limited by the number of configured repository
profiles. Each profile has its own registration and work folder configuration.
