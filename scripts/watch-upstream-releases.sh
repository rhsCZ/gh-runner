#!/usr/bin/env bash

set -euo pipefail

upstream_owner=actions
upstream_repo=runner
release_tag=
target_branch=

usage() {
    cat <<'EOF'
Usage: scripts/watch-upstream-releases.sh [options]

Checks the latest upstream runner release against the local imported branch and
writes GitHub Actions outputs describing whether import/build should run.

Options:
  --upstream-owner <owner>  Upstream GitHub owner (default: actions)
  --upstream-repo <repo>    Upstream GitHub repository (default: runner)
  --release-tag <tag>       Specific release tag to check; defaults to latest
  --target-branch <branch>  Override the local target branch
  --help                    Show this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --upstream-owner) upstream_owner=$2; shift 2 ;;
        --upstream-repo) upstream_repo=$2; shift 2 ;;
        --release-tag) release_tag=$2; shift 2 ;;
        --target-branch) target_branch=$2; shift 2 ;;
        --help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
    esac
done

if ! command -v gh >/dev/null 2>&1; then
    echo "gh CLI is required." >&2
    exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
    echo "jq is required." >&2
    exit 1
fi

if [[ -z "${release_tag}" ]]; then
    release_tag=$(gh api "repos/${upstream_owner}/${upstream_repo}/releases/latest" --jq '.tag_name')
fi

if [[ ! "${release_tag}" =~ ^v?([0-9]+)\.([0-9]+)\.([0-9]+) ]]; then
    echo "Unsupported runner release tag format: ${release_tag}" >&2
    exit 1
fi

release_version="${BASH_REMATCH[1]}.${BASH_REMATCH[2]}.${BASH_REMATCH[3]}"
upstream_branch="releases/m${BASH_REMATCH[2]}"
if [[ -z "${target_branch}" ]]; then
    target_branch="${upstream_branch}"
fi

upstream_git_url="https://github.com/${upstream_owner}/${upstream_repo}.git"
upstream_commit=$(git ls-remote "${upstream_git_url}" "refs/heads/${upstream_branch}" | awk '{print $1}')
if [[ -z "${upstream_commit}" ]]; then
    echo "Could not resolve upstream branch ${upstream_branch}." >&2
    exit 1
fi

imported_commit=
if git ls-remote --exit-code origin "refs/heads/${target_branch}" >/dev/null 2>&1; then
    git fetch --no-tags origin "${target_branch}" >/dev/null
    imported_commit=$(git show FETCH_HEAD:config/upstream-import.json 2>/dev/null | jq -r '.commit // empty' || true)
fi

branch_slug=$(printf '%s' "${target_branch}" | tr '[:upper:]/' '[:lower:]-' | sed -E 's/[^a-z0-9._-]+/-/g; s/^-+|-+$//g')
release_name="runner-v${release_version}-${branch_slug}"

needs_import=false
if [[ "${imported_commit}" != "${upstream_commit}" ]]; then
    needs_import=true
fi

release_exists=false
if gh release view "${release_name}" >/dev/null 2>&1; then
    release_exists=true
fi

needs_build=false
if [[ "${needs_import}" == true || "${release_exists}" == false ]]; then
    needs_build=true
fi

{
    printf 'release_tag=%s\n' "${release_tag}"
    printf 'release_version=%s\n' "${release_version}"
    printf 'upstream_branch=%s\n' "${upstream_branch}"
    printf 'target_branch=%s\n' "${target_branch}"
    printf 'upstream_commit=%s\n' "${upstream_commit}"
    printf 'imported_commit=%s\n' "${imported_commit}"
    printf 'release_name=%s\n' "${release_name}"
    printf 'needs_import=%s\n' "${needs_import}"
    printf 'needs_build=%s\n' "${needs_build}"
} >> "${GITHUB_OUTPUT:-/dev/stdout}"

echo "Upstream release: ${release_tag}"
echo "Upstream branch: ${upstream_branch}"
echo "Target branch: ${target_branch}"
echo "Upstream commit: ${upstream_commit}"
echo "Imported commit: ${imported_commit:-<none>}"
echo "Release: ${release_name} (exists: ${release_exists})"
echo "Needs import: ${needs_import}"
echo "Needs build: ${needs_build}"
