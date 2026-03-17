# Escalation Rules

## Escalate Immediately When

- upper-layer test reveals lower-layer correctness defect
- defect forces non-trivial workaround in feature code
- defect affects shared config/data contracts
- defect can cause silent divergence from documented gameplay behavior

## Escalation Packet

Include:
- debt id
- impact level and severity
- minimal repro
- fuse decision
- owning subsystem

## Blocking Policy

- P0: block merge unless explicit waiver exists.
- P1: block release branch; allow dev branch only with fuse + owner + due window.
- P2/P3: allow merge with tracked debt item and evidence links.

