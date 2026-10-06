#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)

source "${REPO_ROOT}/config/upstream.env"

repository=${UPSTREAM_REPOSITORY}
branch=${UPSTREAM_BRANCH}
ref=${UPSTREAM_REF}
dest=${RUNNER_DEST}

usage() {
    cat <<'EOF'
Usage: scripts/sync-upstream.sh [options]

Options:
  --repo <url>       Upstream git repository URL
  --branch <name>    Upstream branch to sync
  --ref <ref>        Specific ref/commit/tag to check out after fetch
  --dest <path>      Destination directory for the local working snapshot
  --help             Show this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --repo)
            repository=$2
            shift 2
            ;;
        --branch)
            branch=$2
            shift 2
            ;;
        --ref)
            ref=$2
            shift 2
            ;;
        --dest)
            dest=$2
            shift 2
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

if [[ -z "${branch}" && -z "${ref}" ]]; then
    echo "Either --branch, --ref, or UPSTREAM_BRANCH must be set." >&2
    exit 1
fi

dest_abs="${REPO_ROOT}/${dest}"
tmp_dir=$(mktemp -d)
source_dir="${tmp_dir}/source"
trap 'rm -rf "${tmp_dir}"' EXIT

echo "Syncing ${repository} branch ${branch} into ${dest_abs}"

resolved_ref=${ref:-$branch}
commit_sha=

sync_via_git() {
    mkdir -p "${source_dir}"
    GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" init -q
    GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" remote add origin "${repository}"
    GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" fetch --depth 1 origin "${branch}"

    local checkout_ref="FETCH_HEAD"
    if [[ -n "${ref}" ]]; then
        GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" fetch --depth 1 origin "${ref}"
    fi

    GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" checkout -q "${ref:-${checkout_ref}}"
    commit_sha=$(GIT_TERMINAL_PROMPT=0 git -C "${source_dir}" rev-parse HEAD)
}

sync_via_github_archive() {
    local normalized=${repository%.git}
    local repo_path
    repo_path=$(printf '%s\n' "${normalized}" | sed -E 's#^(git@github.com:|https://github.com/)##')
    if [[ ! "${repo_path}" =~ ^[^/]+/[^/]+$ ]]; then
        echo "Git fallback failed and archive fallback only supports github.com repositories." >&2
        exit 1
    fi

    local archive_url="https://codeload.github.com/${repo_path}/tar.gz/${resolved_ref}"
    local archive_file="${tmp_dir}/runner.tar.gz"
    local extract_dir="${tmp_dir}/extract"

    echo "Falling back to GitHub archive ${archive_url}"
    curl -fsSL "${archive_url}" -o "${archive_file}"
    mkdir -p "${extract_dir}"
    tar -xzf "${archive_file}" -C "${extract_dir}" --strip-components=1
    mkdir -p "${source_dir}"
    rsync -a "${extract_dir}/" "${source_dir}/"

    if command -v jq >/dev/null 2>&1; then
        commit_sha=$(curl -fsSL "https://api.github.com/repos/${repo_path}/commits/${resolved_ref}" | jq -r '.sha')
    else
        commit_sha="${resolved_ref}"
    fi
}

if ! sync_via_git; then
    rm -rf "${source_dir}"
    sync_via_github_archive
fi

mkdir -p "${dest_abs}"
rsync -a --delete \
    --exclude='.git/' \
    --exclude='.github/workflows/' \
    "${source_dir}/" "${dest_abs}/"

rm -rf "${dest_abs}/.github/workflows"

synced_at=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

cat > "${dest_abs}/.upstream-sync.json" <<EOF
{
  "repository": "${repository}",
  "branch": "${branch}",
  "ref": "${ref:-${branch}}",
  "commit": "${commit_sha}",
  "synced_at_utc": "${synced_at}"
}
EOF

echo "Upstream sync complete at ${commit_sha}"
