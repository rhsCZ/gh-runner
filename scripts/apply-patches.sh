#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "${SCRIPT_DIR}/.." && pwd)
PATCH_DIR="${REPO_ROOT}/patches"
RUNNER_DIR="${REPO_ROOT}/.work/actions-runner"

dry_run=0
commit_patches=0

usage() {
    cat <<'EOF'
Usage: scripts/apply-patches.sh [options]

Options:
  --source-dir <path>  Runner source tree (default: .work/actions-runner)
  --dry-run            Validate patches without changing files
  --commit             Create one local git commit for each applied patch
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source-dir)
            RUNNER_DIR=$2
            shift 2
            ;;
        --dry-run)
            dry_run=1
            shift
            ;;
        --commit)
            commit_patches=1
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

if [[ ! -d "${RUNNER_DIR}" ]]; then
    echo "Runner source directory not found: ${RUNNER_DIR}" >&2
    echo "Run scripts/sync-upstream.sh first or pass --source-dir." >&2
    exit 1
fi

declare -a patch_files=()

if [[ -f "${PATCH_DIR}/series.txt" ]]; then
    while IFS= read -r line; do
        [[ -z "${line}" ]] && continue
        [[ "${line}" =~ ^# ]] && continue
        patch_files+=("${PATCH_DIR}/${line}")
    done < "${PATCH_DIR}/series.txt"
fi

if [[ ${#patch_files[@]} -eq 0 ]]; then
    while IFS= read -r file; do
        patch_files+=("${file}")
    done < <(find "${PATCH_DIR}" -maxdepth 1 -type f -name '*.patch' | sort)
fi

if [[ ${#patch_files[@]} -eq 0 ]]; then
    echo "No patch files found in ${PATCH_DIR}; skipping."
    exit 0
fi

patch_target=${RUNNER_DIR}
if [[ ${dry_run} -eq 1 ]]; then
    validation_dir=$(mktemp -d)
    trap 'rm -rf "${validation_dir}"' EXIT
    cp -a "${RUNNER_DIR}/." "${validation_dir}/"
    patch_target=${validation_dir}
fi

for patch_file in "${patch_files[@]}"; do
    if [[ ! -f "${patch_file}" ]]; then
        echo "Missing patch file: ${patch_file}" >&2
        exit 1
    fi

    echo "Applying $(basename "${patch_file}")"
    if [[ ${dry_run} -eq 1 ]]; then
        patch --batch --forward --fuzz=3 -d "${patch_target}" -p1 < "${patch_file}"
    else
        patch --batch --forward --fuzz=3 -d "${patch_target}" -p1 < "${patch_file}"
        if [[ ${commit_patches} -eq 1 ]]; then
            git -C "${RUNNER_DIR}" add -A
            git -C "${RUNNER_DIR}" commit -m "Apply $(basename "${patch_file}" .patch)"
        fi
    fi
done

echo "Patch application complete."
