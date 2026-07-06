---
name: real-work
description: Use when planning a multi-step task or working in plan mode and you need to capture the plan as a durable, resumable artifact — breaking work into phases with per-item checkboxes, completion tracking, autonomous verification, and a handoff summary so a future agent can pick up where you left off. Use when user wants to create or design a plan or mentioned "real work".
---

# Real Work

Turn planning into a durable, resumable artifact. The plan file — not the
conversation — is the source of truth: it records what to do, what's done, how it
was verified, and how to deploy. Any future agent can resume from it with zero
prior context.

**Use when** planning multi-step / multi-session work that may outlive the current
session. Skip for trivial single-session tasks.

## 1. Reach complete understanding first

Do **not** write the plan until scope is fully understood. Ask questions until
you both share a complete understanding with **no gaps**.

- Maximum 2 rounds of questions. If ambiguity remains after that, document it
  as a hypothesis in the plan and proceed.
- Surface every assumption for the user to confirm.
- Summarize the full scope back and only proceed once the user confirms.

## 2. Write the plan

Save to `plans/<descriptive-name>.md` in the repository root.

```markdown
# <Work Title>

<1-2 sentence goal and scope.>

## For Future Agents
As work proceeds: mark checkboxes `- [x]` as items complete; when a phase is done,
set its status to `Complete` and write its **Phase Summary**; run the phase's
**Verification Plan** and record the result before moving on.

## Phase 1: <Title>
Status: Not started

- [ ] <concrete, actionable item>
- [ ] <concrete, actionable item>

### Verification Plan
- <command the agent can run autonomously, with expected result>

### Phase Summary
_(write when phase completes)_

## Final Recap
_(write when all phases complete)_

## Deployment Plan
_(write when all phases complete)_
```

## Common mistakes

- **Vague items** — each checkbox is a concrete task, not a theme.
- **Non-autonomous verification** — give runnable commands with expected output.
- **Pre-filling summaries** — stay as placeholders until work actually completes.
