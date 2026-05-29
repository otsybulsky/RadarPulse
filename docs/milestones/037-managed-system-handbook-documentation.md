# Milestone 037: Managed System Handbook Documentation

Status: active.

Milestone 037 starts after the closed milestone 036 clean architecture
hardening milestone. RadarPulse leaves freeze mode for a documentation-only
milestone whose goal is to make the accepted system understandable as a
managed handbook, not as one unbounded encyclopedia page.

Milestone 036 closed with this posture:

```text
accepted with scoped warnings for clean architecture hardening toward a
defensible 10/10 posture over the accepted local product demo/runtime
boundary
```

The important shift is:

```text
from:
  RadarPulse has strong implementation, tests, milestone evidence, and code
  contract comments, but complete understanding still requires moving through
  many milestone documents and source folders

to:
  RadarPulse has a guided handbook that starts from purpose and mental model,
  then progressively leads readers through architecture, workflows, modules,
  contracts, implementation details, diagnostics, and change points
```

This milestone is documentation architecture and handbook implementation. It
does not reopen runtime, product, HTTP, persistence, UI, demo/readiness, or
clean architecture decisions.

## Milestone Goal

Create a managed system handbook that gives a new reader a reliable path from
high-level understanding to implementation-level confidence.

The handbook should explain RadarPulse in plain language while preserving
technical accuracy:

```text
what RadarPulse is and why it exists
how the accepted local product demo/runtime boundary is shaped
how the major layers, projects, and responsibilities fit together
how data moves from archive input through streaming batches, processing,
handlers, durable/queueing paths, product read models, HTTP, CLI, and UI
how accepted runtime postures, fallbacks, diagnostics, and warnings should be
  interpreted
where a maintainer should go to change a specific behavior
which milestone documents remain the source of historical decisions and gate
  evidence
```

The handbook is managed documentation. That means it should have explicit
structure, reader paths, ownership rules, update rules, and verification
habits so it can evolve with the system.

## Managed Documentation Principles

The handbook should be built around these rules:

```text
progressive disclosure:
  start with the simplest useful model, then link deeper pages for details

single current entrypoint:
  one handbook index explains what to read first and where each topic lives

reader paths:
  support product/demo readers, architecture reviewers, maintainers, and
  implementation contributors without forcing all readers through every page

current-state first:
  describe the accepted current system directly, while linking historical
  milestone documents as evidence instead of rewriting them

plain-language explanations:
  explain concepts before type names, then point to concrete source folders,
  contracts, and tests

bounded pages:
  prefer focused pages with clear purpose over one large book page

update discipline:
  each future behavior or architecture change should update the relevant
  handbook page or explicitly record why no handbook update is needed
```

## Scope Rules

Safe in this milestone:

```text
create docs/handbook as the current system handbook area
add a handbook README/table of contents and reader paths
add a current system overview and vocabulary/glossary
add source navigation for the accepted responsibility-first project layout
document major workflows and data flows in plain language
document architecture/layer boundaries and accepted dependency direction
document runtime, queueing, durable, processing, handler, product, HTTP, CLI,
  UI, demo, diagnostics, and verification postures at the level useful for
  maintainers
link to milestone closeouts, decision traces, gates, tests, and source files
  as supporting evidence
add documentation quality checks that are proportional to documentation-only
  work
update README/project-progress/handoff links when the handbook becomes useful
```

Not safe in this milestone unless explicitly reprioritized:

```text
change runtime behavior, product API shape, CLI output, HTTP contract, UI
  workflow, persistence format, or demo scripts as a hidden side effect
rewrite historical milestone evidence
replace milestone closeouts, decision traces, or handoff as the historical
  record
claim production deployment, security, live ingestion, external adapter, or
  exactly-once readiness beyond accepted scope
turn the handbook into generated API reference or line-by-line source
  commentary
make documentation completeness block unrelated maintenance fixes forever
```

## Handbook Shape

The expected handbook structure can evolve, but the initial direction is:

```text
docs/handbook/README.md
  entrypoint, reading paths, page map, and maintenance rules

docs/handbook/system-overview.md
  purpose, current accepted scope, main concepts, and mental model

docs/handbook/source-map.md
  project/layer/folder navigation after milestone 034 and 036

docs/handbook/workflows.md
  end-to-end product/demo/runtime workflows in plain language

docs/handbook/architecture.md
  layer boundaries, ports/adapters, dependency rules, and guardrails

docs/handbook/processing-runtime.md
  streaming batches, processing core, queueing, durable, ordering, handlers,
  rebalance, retention, pressure, diagnostics, and accepted fallback posture

docs/handbook/product-surface.md
  CLI, HTTP, product API/read models, run history, Operator UI, and local demo

docs/handbook/verification-and-evidence.md
  how to verify the current system and where milestone evidence lives

docs/handbook/glossary.md
  short definitions for project-specific terms
```

The exact page list should be refined during the plan slices. The important
constraint is that the handbook remains navigable and updateable.

## Expected Evidence

Because milestone 037 is documentation-only, primary evidence should be
documentation quality evidence:

```text
handbook files exist and link together
current source paths referenced by the handbook exist
historical milestone links referenced by the handbook exist
no stale direct .cs paths are introduced in current handbook pages
markdown formatting/whitespace checks pass
README, project-progress, and handoff point to the handbook when useful
no runtime gate is required unless code, scripts, UI, or product behavior is
  touched
```

## Change Log

### Change 1: Open Managed System Handbook Documentation Milestone

Status: complete.

Intent:

```text
open a documentation-only milestone for a managed handbook that explains the
accepted RadarPulse system from purpose through implementation details
```

Scope:

```text
docs/milestones/037-managed-system-handbook-documentation.md
docs/milestones/037-managed-system-handbook-documentation-plan.md
docs/project-progress.md
docs/handoff.md
```

Verification:

```text
documentation-only opening change; runtime gate deferred unless later slices
touch code, scripts, UI, product behavior, or executable contracts
```

### Change 2: Handbook Entrypoint And Management Rules

Status: complete.

Intent:

```text
create the managed handbook entrypoint, reader paths, page map, update rules,
linking rules, and documentation verification habit
```

Scope:

```text
docs/handbook/README.md
docs/milestones/037-managed-system-handbook-documentation.md
docs/milestones/037-managed-system-handbook-documentation-plan.md
docs/handoff.md
```

Verification:

```text
handbook README created with only implemented or explicitly planned pages
git diff --check passed
```

### Change 3: Current System Map

Status: complete.

Intent:

```text
make the accepted repository layout understandable after the milestone 034
responsibility-first structure and milestone 036 clean architecture hardening
```

Scope:

```text
docs/handbook/source-map.md
docs/handbook/README.md
docs/milestones/037-managed-system-handbook-documentation.md
docs/milestones/037-managed-system-handbook-documentation-plan.md
docs/handoff.md
```

Verification:

```text
referenced source folders exist
referenced current documentation files exist
git diff --check passed
```
