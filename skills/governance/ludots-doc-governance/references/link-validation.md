# Link Validation

Use this policy for path checks in docs.

## Allowed Reference Forms

- Markdown links: `[text](relative/path.md)`
- Backtick paths: `` `docs/...` ``, `` `src/...` ``, `` `assets/...` ``

## Disallowed Forms

- Local absolute paths (`C:\...`, `D:\...`)
- `file://` links
- Stale migration placeholders that look final but do not exist

## Validation Heuristics

1. Treat markdown links and backtick paths as separate channels.
2. Ignore placeholders only when clearly templated:
- contains `{...}` or `...` wildcard notation
3. Everything else must resolve to an existing repository path.

## Suggested Command Pattern

- Use `rg --files` to build target index.
- Parse markdown links and backtick path tokens.
- Emit unresolved items with source file + referenced path.

