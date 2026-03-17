# Doc Governance Checklist

## 1. Scope

- Use repository-relative references only.
- Exclude external reference bodies unless explicitly requested.

## 2. Directory and Entry Rules

- Each subsystem keeps one entry doc: `00_总览.md`.
- Root compatibility entries must point to existing canonical paths.
- Avoid duplicate SSOT claims across sibling documents.

## 3. Evidence Rules

- Every strong claim links to at least one concrete path.
- Prefer code path + test path + alignment path when available.
- Do not use unverifiable summary statements.

## 4. Consistency Rules

- Terms are stable across docs (same concept, same term).
- Status fields are updated when content is production-ready.
- KANBAN acceptance criteria must map to existing evidence paths.

## 5. Output Rules

- Findings are sorted by severity (P0, P1, P2, P3).
- Each finding contains: problem, impact, evidence, recommendation.

