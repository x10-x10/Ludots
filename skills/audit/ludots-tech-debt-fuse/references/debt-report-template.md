# Tech Debt Report Template

```md
# Tech Debt Report: <id>

Date: <YYYY-MM-DD>
Reporter:
Owner:
Severity: <P0|P1|P2|P3>
Scope: <Local|Subsystem|Cross-layer|Global>

## Trigger
- Scenario:
- Entry point:
- Repro steps:

## Evidence
- `src/...`
- `docs/...`
- `tests/...`

## Impact
- User-visible impact:
- Correctness/stability risk:
- Blast radius:

## Fuse Decision
- Mode: <hard-stop|explicit-degrade|isolation>
- Reason:
- Observability fields:

## Containment and Follow-up
- Immediate containment:
- Permanent fix direction:
- Target milestone:
```

