#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_PROFILE="${1:-default}"
TARGET_DIR="${ROOT_DIR}/.runner.d/${TARGET_PROFILE}"

FILES=(
  ".runner"
  ".credentials"
  ".credentials_rsaparams"
  ".runner_migrated"
  ".credentials_migrated"
)

if [[ ! -f "${ROOT_DIR}/.runner" || ! -f "${ROOT_DIR}/.credentials" ]]; then
  echo "Root runner configuration was not found. Expected '${ROOT_DIR}/.runner' and '${ROOT_DIR}/.credentials'."
  exit 1
fi

if [[ -e "${TARGET_DIR}" ]]; then
  echo "Target profile '${TARGET_PROFILE}' already exists at '${TARGET_DIR}'."
  exit 1
fi

mkdir -p "${TARGET_DIR}"

for file in "${FILES[@]}"; do
  if [[ -f "${ROOT_DIR}/${file}" ]]; then
    cp -p "${ROOT_DIR}/${file}" "${TARGET_DIR}/${file}"
  fi
done

echo "Copied root runner configuration to '${TARGET_DIR}'."
echo "Root files were left in place. After validating multi-repo mode, remove them manually if desired."
