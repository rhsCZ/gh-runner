# Patches

This directory contains forward-port patches applied to the imported
`actions/runner` source tree.

## Current Order

- `0001-multi-repo-core-and-windows-fixes.patch` ports commit `430a03bd` from the custom `releases/m337` line and includes the multi-repository runner changes plus both Windows overwrite fixes.
- `0002-parallel-multi-repo-and-shared-options.patch` ports commit `35d7bd10` and includes parallel repository dispatch, isolated worker configuration, shared `.options`, and `config set` support.

## Application

`series.txt` defines the deterministic patch order. If it is absent, patch files
are applied alphabetically.

The patcher uses `patch --forward --fuzz=3`. This is intentionally more tolerant
than exact `git apply` context matching, but it still stops with a non-zero exit
code when a hunk cannot be applied.
