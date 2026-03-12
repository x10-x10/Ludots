# Doc Governance Report

## Scope

- `docs/reference/cli_runbook.md`
- `docs/conventions/03_environment_setup.md`
- `docs/architecture/startup_entrypoints.md`

## Rule Set Used

- wrapper commands must match `scripts/run-mod-launcher.cmd` and `scripts/run-mod-launcher.ps1`
- launcher CLI examples must use the canonical `.\scripts\run-mod-launcher.cmd cli ...` form
- docs must point to current product entrypaths, not deprecated WPF launcher flows
- links must use repository-relative paths

## Findings

- Fixed: previously corrupted CLI-facing docs were rewritten into clean ASCII text.
- Fixed: command examples now consistently use the canonical wrapper invocation.
- Fixed: docs now describe web launcher and CLI as the product entry surface sharing one backend.
- Fixed: docs now call out current web performance debt explicitly and link the debt report.

## Fix Order

1. Restore readable SSOT documents for CLI usage.
2. Align command examples with wrapper and entrypoint implementation.
3. Link the current web performance debt so product status is explicit.
