# RadarPulse System Handbook

Status: active under milestone 037.

This handbook is the current-system guide for RadarPulse. It explains the
accepted system from purpose and mental model down to implementation
navigation, without replacing milestone closeouts, decision traces, or gate
evidence.

Use this handbook when you want to understand how RadarPulse works today.
Use milestone documents when you need to understand why a decision was made,
what evidence accepted it, or what warnings were carried forward.

## Reading Paths

Product/demo reader:

```text
start:
  system overview
then:
  product surface
  workflows
  verification and evidence
goal:
  understand what the local portfolio demo does, how to run it, and what its
  readiness output means
```

Architecture reviewer:

```text
start:
  system overview
then:
  architecture
  source map
  processing runtime
  verification and evidence
goal:
  understand layer boundaries, ports/adapters, dependency direction, runtime
  postures, and accepted scope limits
```

Maintainer:

```text
start:
  source map
then:
  workflows
  processing runtime
  product surface
  glossary
goal:
  know where behavior lives, where tests live, and which handbook page should
  change with a future behavior change
```

Implementation contributor:

```text
start:
  workflows
then:
  architecture
  processing runtime
  source map
  verification and evidence
goal:
  understand the end-to-end flow before changing code, then select the right
  focused test or gate for the touched surface
```

## Page Map

Current page:

```text
docs/handbook/README.md
  handbook entrypoint, reading paths, and management rules
```

Implemented pages:

```text
docs/handbook/system-overview.md
  purpose, current accepted scope, main concepts, and mental model

docs/handbook/source-map.md
  solution, source, and test navigation for the current responsibility-first
  layout

docs/handbook/glossary.md
  short definitions for RadarPulse-specific terms

docs/handbook/workflows.md
  end-to-end product/demo/runtime flows in plain language

docs/handbook/architecture.md
  layer boundaries, ports/adapters, dependency rules, and architecture
  guardrails

docs/handbook/processing-runtime.md
  streaming batches, processing core, queueing, durable/recovery, ordering,
  handlers, rebalance, retention, pressure, diagnostics, and fallback posture

docs/handbook/product-surface.md
  CLI, HTTP, product API/read models, run history, Operator UI, local demo,
  and same-origin delivery

docs/handbook/verification-and-evidence.md
  focused gates, package verification, milestone evidence, and known scope
  limits
```

Planned pages for milestone 037:

```text
all planned content pages are now implemented; slice 7 will integrate links
and run the handbook quality pass
```

Until a planned page exists, the milestone 037 plan is the source for its
intended scope.

## What Belongs Here

The handbook should describe the accepted current system directly:

```text
current behavior and current architecture
plain-language explanations of project concepts
source and test navigation
workflow descriptions
maintainer-oriented change points
links to current docs, milestone closeouts, decision traces, and gates
accepted warnings and scope limits that affect current interpretation
```

The handbook should not become:

```text
a replacement for historical milestone evidence
a generated API reference
line-by-line source commentary
a place to reopen accepted runtime, product, HTTP, CLI, UI, persistence, or
  demo behavior decisions
a production-readiness claim beyond the accepted local product demo/runtime
  boundary
```

## Update Rules

When future work changes behavior or architecture:

```text
update the relevant handbook page in the same milestone or commit
if no handbook update is needed, say why in the milestone note or handoff
keep current-state explanations direct and move historical detail to links
prefer one focused page update over broad wording churn
avoid duplicating full milestone evidence; link to it instead
check direct source paths when adding or moving path references
```

When future work changes only tests, comments, formatting, or evidence:

```text
update the handbook only if reader behavior, maintainer navigation, or
accepted interpretation changes
otherwise keep the milestone/handoff record as the evidence trail
```

## Link Rules

Use links for implemented pages and stable current documents. For planned
pages, list the path as text until the page exists.

Prefer links to:

```text
current handbook pages
current README/product demo docs
milestone closeouts and decision traces
source folders instead of individual files when the folder is the real
  navigation target
focused tests or test folders when they are the best verification entrypoint
```

Avoid deep links to individual implementation files unless a page is
explaining a specific contract, entrypoint, or guardrail.

## Verification Habit

Documentation-only handbook changes normally require:

```text
git diff --check
handbook path/link spot-checks for newly referenced files
no runtime gate unless code, scripts, UI, product behavior, or executable
  contracts changed
```

Code or behavior changes that also update the handbook should use the gate
for the changed behavior, not a handbook-only gate.
