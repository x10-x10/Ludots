---
name: ludots-pr-issue-orchestration
description: Assemble Ludots Issue and PR packets from handoff, visual review, and CI evidence. Use when preparing reviewer-ready change descriptions or issue updates.
---

# Ludots PR / Issue Orchestration

Use this skill to build a review-ready packet for PRs and Issues.

## Load References

1. Read `references/pr-issue-packet-spec.md`.
2. Read `../../README.md` when hook dependencies need to be checked.

## Mandatory Rules

1. PR and issue packets must be evidence-backed.
- Do not summarize behavior that is not linked to tests, captures, reviews, or CI packets.

2. One subject, one packet.
- Keep a single packet per PR or issue subject.
- Update by replacing the packet contents, not by creating parallel summaries.

3. Visual work must carry visual evidence.
- For UI or rendering changes, request or reference `visual.review.completed`.

## Workflow

1. Identify the subject: PR, issue, or review thread.
2. Gather handoff, visual review, and CI hook packets.
3. Build reviewer summary, change list, risks, and validation checklist.
4. Write packet:
- `artifacts/pr/<subject>/packet.md`
- `artifacts/agent-hooks/<subject>-pr.packet.ready.json`
5. Emit `pr.packet.ready`.

## Output Requirements

Provide:
- reviewer summary
- exact changed-file clusters
- validation and evidence checklist
- outstanding blockers or follow-ups
