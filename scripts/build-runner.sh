#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
RUNNER_DIR="${REPO_ROOT}/.work/actions-runner"

configuration=Release
runtime=
package=0

usage() {
    cat <<'EOF'
Usage: scripts/build-runner.sh --runtime <rid> [options]

Options:
  --source-dir <path>  Runner source tree (default: .work/actions-runner)
  --configuration <c>  Build configuration (default: Release)
  --package            Create the distributable package
EOF
}

ensure_runner_git_context() {
    if [[ -d "${RUNNER_DIR}/.git" ]]; then
        return
    fi

    git -C "${RUNNER_DIR}" init -q
    git -C "${RUNNER_DIR}" config user.name "gh-runner-build"
    git -C "${RUNNER_DIR}" config user.email "gh-runner-build@local"
    git -C "${RUNNER_DIR}" add -A
    git -C "${RUNNER_DIR}" commit -q -m "Vendor snapshot for build" || true
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source-dir)
            RUNNER_DIR=$2
            shift 2
            ;;
        --runtime)
            runtime=$2
            shift 2
            ;;
        --configuration)
            configuration=$2
            shift 2
            ;;
        --package)
            package=1
            shift
            ;;
        --help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if [[ -z "${runtime}" ]]; then
    echo "--runtime is required" >&2
    usage >&2
    exit 1
fi

if [[ ! -d "${RUNNER_DIR}/src" ]]; then
    echo "Runner source directory not found: ${RUNNER_DIR}" >&2
    echo "Run scripts/sync-upstream.sh first." >&2
    exit 1
fi

ensure_runner_git_context

pushd "${RUNNER_DIR}/src" >/dev/null
if [[ "${runtime}" == win-* ]]; then
    cmd.exe //D //S //C "dev.cmd layout ${configuration} ${runtime}"
else
    ./dev.sh layout "${configuration}" "${runtime}"
fi

if [[ ${package} -eq 1 ]]; then
    if [[ "${runtime}" == win-* ]]; then
        cmd.exe //D //S //C "dev.cmd package ${configuration} ${runtime}"
    else
        ./dev.sh package "${configuration}" "${runtime}"
    fi
fi
popd >/dev/null
