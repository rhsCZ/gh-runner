#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
source "${REPO_ROOT}/config/upstream.env"

repository=${UPSTREAM_REPOSITORY}
branch=${UPSTREAM_BRANCH}
ref=${UPSTREAM_REF:-}
target_branch=
control_ref=master

usage() {
    cat <<'EOF'
Usage: scripts/import-upstream-history.sh --target-branch <branch> [options]

Creates a local branch from the complete upstream history, then adds one local
overlay commit which removes upstream workflows and restores this repository's
orchestration files. Local patches are committed individually on top.

Options:
  --repo <url>            Upstream Git repository URL
  --branch <name>         Upstream branch to import
  --ref <ref>             Optional upstream commit/tag; defaults to --branch
  --target-branch <name>  Local branch to create or replace
  --control-ref <ref>     Ref containing orchestration files (default: master)
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --repo) repository=$2; shift 2 ;;
        --branch) branch=$2; shift 2 ;;
        --ref) ref=$2; shift 2 ;;
        --target-branch) target_branch=$2; shift 2 ;;
        --control-ref) control_ref=$2; shift 2 ;;
        --help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
    esac
done

if [[ -z "${target_branch}" ]]; then
    echo "--target-branch is required." >&2
    exit 1
fi

if [[ -z "${branch}" && -z "${ref}" ]]; then
    echo "Either --branch, --ref, or UPSTREAM_BRANCH must be set." >&2
    exit 1
fi

git check-ref-format --branch "${target_branch}" >/dev/null
git rev-parse --verify "${control_ref}^{commit}" >/dev/null

upstream_ref=${ref:-${branch}}
worktree_dir=$(mktemp -d)
trap 'git worktree remove --force "${worktree_dir}" >/dev/null 2>&1 || true; rm -rf "${worktree_dir}"' EXIT

echo "Fetching complete history for ${repository} ${upstream_ref}"
GIT_TERMINAL_PROMPT=0 git fetch --no-tags "${repository}" "${upstream_ref}"
upstream_commit=$(git rev-parse FETCH_HEAD)

git worktree add --detach "${worktree_dir}" "${upstream_commit}" >/dev/null
rm -rf "${worktree_dir}/.github/workflows"

# The target keeps upstream source history, but only our checked-in automation.
git archive "${control_ref}" .github/workflows config patches scripts README.md .gitignore | tar -x -C "${worktree_dir}"
mkdir -p "${worktree_dir}/config"
cat > "${worktree_dir}/config/upstream-import.json" <<EOF
{
  "repository": "${repository}",
  "branch": "${branch}",
  "ref": "${upstream_ref}",
  "commit": "${upstream_commit}"
}
EOF
git -C "${worktree_dir}" add -A -- .github/workflows config patches scripts README.md .gitignore
git -C "${worktree_dir}" -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" \
    commit -m "Overlay local orchestration for ${branch}"

"${worktree_dir}/scripts/apply-patches.sh" --source-dir "${worktree_dir}" --commit
git branch -f "${target_branch}" HEAD

echo "Imported ${upstream_commit} into ${target_branch}"
