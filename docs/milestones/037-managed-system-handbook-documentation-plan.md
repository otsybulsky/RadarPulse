# Milestone 037: Managed System Handbook Documentation Implementation Plan

Status: active.

This plan implements the milestone 037 documentation boundary. The milestone
creates a managed handbook that explains RadarPulse from the simplest useful
mental model down to implementation details, while preserving milestone
closeouts and decision traces as the historical evidence record.

## Implementation Strategy

Build the handbook in small documentation slices:

```text
first create the handbook entrypoint and rules
then map the current source/documentation system
then write the high-level overview and vocabulary
then document the most important workflows and architecture boundaries
then add deeper runtime/product/module pages
then add verification/evidence guidance and current documentation links
then review for stale paths, missing reader paths, and unclear explanations
```

Each slice should leave the handbook useful on its own. The first version
does not need to explain every class. It should make the system navigable and
give future updates a clear place to land.

## Slice 1: Handbook Entrypoint And Management Rules

Status: complete.

Goal:

```text
create docs/handbook as the managed current-system documentation area
```

Planned changes:

```text
add docs/handbook/README.md
define reader paths:
  product/demo reader
  architecture reviewer
  maintainer
  implementation contributor
define handbook update rules
define what belongs in handbook pages versus milestone evidence
define page naming and linking conventions
```

Verification:

```text
handbook README links only to existing or planned pages clearly marked as
  planned
git diff --check
```

## Slice 2: Current System Map

Status: queued.

Goal:

```text
make the accepted repository layout understandable after the responsibility
folder structure and clean architecture hardening work
```

Planned changes:

```text
add source-map page
explain solution projects and layer responsibilities
map major source folders to the concepts they implement
map major test folders to the behaviors they verify
link to README and current milestone 034/036 evidence where useful
```

Verification:

```text
referenced source folders exist
referenced current documentation files exist
git diff --check
```

## Slice 3: System Overview And Glossary

Status: queued.

Goal:

```text
give readers the first complete mental model before they enter detailed
runtime or source pages
```

Planned changes:

```text
add system-overview page
add glossary page
explain RadarPulse purpose, accepted local product demo/runtime scope, and
  main concepts in plain language
define project-specific terms such as archive-shaped workload, provider
  sequence, retained payload, handler posture, BFF/read model, durable
  envelope, ordered commit, and readiness
```

Verification:

```text
terms used in overview link to glossary or deeper pages
git diff --check
```

## Slice 4: Core Workflows

Status: queued.

Goal:

```text
document the major end-to-end flows before documenting individual modules
```

Planned changes:

```text
add workflows page
document local demo startup and readiness flow
document archive input to radar event batch flow
document processing and ordered commit flow
document handler output/read-model/product surface flow
document failure/readiness/diagnostic flow
link each workflow to the source-map and deeper topic pages
```

Verification:

```text
workflow source/test/document links exist
git diff --check
```

## Slice 5: Architecture And Runtime Deepening

Status: queued.

Goal:

```text
explain the accepted architecture and runtime postures at maintainer depth
without duplicating every source file
```

Planned changes:

```text
add architecture page
add processing-runtime page
document layer boundaries, ports/adapters, dependency direction, and
  architecture guardrails
document streaming batches, processing core, queueing, durable/recovery,
  ordered commit, rebalance, retention, pressure, handler postures, fallback,
  diagnostics, and accepted warnings
link to milestone 020-036 decisions where they are the source of evidence
```

Verification:

```text
referenced milestone files exist
referenced source folders and guardrail tests exist
git diff --check
```

## Slice 6: Product Surface And Verification Evidence

Status: queued.

Goal:

```text
make the user-facing/product-facing part of the system and its verification
path easy to inspect
```

Planned changes:

```text
add product-surface page
add verification-and-evidence page
document CLI, HTTP, product API/read models, run history, Operator UI,
  same-origin local delivery, package scripts, and accepted demo scope
document which focused gates verify which areas
link to product demo readiness docs, package scripts, tests, and milestone
  gate evidence
```

Verification:

```text
referenced scripts, docs, source folders, and tests exist
git diff --check
```

## Slice 7: Handbook Integration And Quality Pass

Status: queued.

Goal:

```text
make the handbook discoverable and check it for navigability, stale paths,
and clarity
```

Planned changes:

```text
link the handbook from README or current project documentation where useful
update docs/project-progress.md and docs/handoff.md
run current documentation path checks for handbook pages
remove or mark any placeholder links that are not implemented
record remaining handbook gaps as explicit follow-ups
```

Verification:

```text
handbook link graph spot-check passes
current handbook direct-file path audit passes
git diff --check
```

## Slice 8: Decision Trace And Closeout

Status: queued.

Goal:

```text
close the documentation milestone with a clear answer about handbook
readiness and remaining documentation gaps
```

Planned changes:

```text
write decision trace
write closeout
update handoff
update project-progress
record the next recommended documentation or maintenance input
```

Verification:

```text
documentation-only closeout checks
git diff --check
```
