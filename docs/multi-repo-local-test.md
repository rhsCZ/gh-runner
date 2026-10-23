# Multi-Repo Runner Build And Test

This document describes how to build this runner fork, install it on a Linux host, and validate multi-repository dispatch with three repositories.

## Build

From the repository root:

```bash
cd /opt/gh-runner/src
bash dev.sh build Debug
bash dev.sh layout Debug
bash dev.sh package Debug
```

The packaged runner archive will be created under:

```text
/opt/gh-runner/_package/
```

The unpacked runnable layout will be available under:

```text
/opt/gh-runner/_layout/
```

## Install On A Host

Copy or extract the packaged runner to the target machine, then:

```bash
cd /srv/github-runner
tar -xzf actions-runner-*.tar.gz
./bin/installdependencies.sh
```

If migrating an already configured single-repository runner, run the migration script from the source checkout root while that checkout points at the installed runner directory, or copy the script into the installed runner root first:

```bash
cd /opt/gh-runner
./scripts/migrate-root-runner-to-profile.sh default
```

Register three repository profiles on the same runner directory:

```bash
./config.sh add --profile repo-a --url https://github.com/USER/repo-a --token TOKEN_A --name multi-repo-host --labels multi-repo-test
./config.sh add --profile repo-b --url https://github.com/USER/repo-b --token TOKEN_B --name multi-repo-host --labels multi-repo-test
./config.sh add --profile repo-c --url https://github.com/USER/repo-c --token TOKEN_C --name multi-repo-host --labels multi-repo-test
./config.sh list
```

Start the runner:

```bash
./run.sh
```

For service mode after configuration:

```bash
sudo ./svc.sh install
sudo ./svc.sh start
sudo ./svc.sh status
```

## Expected Test Behavior

Use the workflow samples in `examples/multi-repo-workflows/`.

Trigger the workflows in quick succession in this order:

1. `repo-a`
2. `repo-b`
3. `repo-c`

Expected outcome:

1. all three repositories match the same self-hosted runner label
2. only one job runs at a time on the machine
3. the remaining jobs wait
4. after `repo-a` finishes, `repo-b` starts
5. after `repo-b` finishes, `repo-c` starts

To observe ordering, watch:

```text
_diag/Runner_*.log
```

and the workflow logs in GitHub Actions UI.
