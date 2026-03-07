# Reuse-First Policy

## Goal

Prevent per-mod or per-feature reinvention of engine/runtime foundations.

## Rules

1. Reuse existing core abstractions before introducing new stacks.
2. Add extension points in Core when needed; do not duplicate full pipelines in Mods.
3. Reuse existing config catalogs/loaders unless contract mismatch is proven.
4. Reuse existing query/budget/telemetry paths for new gameplay logic.

## Red Flags

- new custom event loop in mod layer
- duplicated config parsing pipeline
- duplicated input/order processing path
- duplicated simulation scheduler

## Decision Record

When adding new infrastructure, record:
- why existing infra is insufficient
- impacted layers
- migration and deprecation plan

