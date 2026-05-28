# Milestone 036: Clean Architecture Hardening Toward 9/10

Status: active.

Milestone 036 starts after the closed milestone 035 code contract
documentation pass. RadarPulse leaves freeze mode for one targeted
behavior-preserving architecture hardening milestone with an explicit quality
goal: raise the current clean architecture, GRASP, SOLID, and GoF assessment
from roughly 7/10 to 9/10 without reopening accepted runtime semantics.

## Milestone Goal

Make the current accepted local product demo architecture materially cleaner
and easier to defend by addressing the largest design issues found in the
architecture review:

```text
move product-facing use-case contracts into the Application layer
keep Infrastructure as adapters and runtime composition behind ports
make Presentation depend on Application contracts rather than concrete
  Infrastructure API classes
add automated architecture tests for project references and namespace
  dependency direction
reduce SRP pressure around product orchestration and presentation entrypoint
  hotspots where changes are behavior-preserving
preserve the accepted product API, CLI behavior, runtime defaults, local
  persistence, and demo/readiness posture
```

The intended outcome is a 9/10 architecture posture, not a broad rewrite. The
score should improve because the most important Clean Architecture and SOLID
risks become mechanically guarded or isolated:

```text
Domain remains pure and dependency-free
Application owns product contracts, use-case vocabulary, and ports
Infrastructure implements adapters over those ports
Presentation maps HTTP/CLI input to Application contracts
architecture tests prevent dependency drift
known large-file/SRP risks are either reduced or explicitly bounded
```

## Scope Rules

Safe in this milestone:

```text
add Application interfaces/ports for product pipeline use cases
move or wrap product API contract behavior into Application-owned contracts
change DI registration to bind Application contracts to Infrastructure
  implementations
split focused helper responsibilities out of product orchestration where it
  keeps public behavior unchanged
add architecture tests for layer and namespace dependency rules
add focused tests around moved contracts and DI wiring
update docs/handoff/progress after every slice
run focused Release build and relevant test gates after each implementation
  slice
```

Not safe in this milestone unless explicitly reprioritized:

```text
change public product DTO/API response shape
change accepted runtime/default pipeline semantics
rewrite processing core, archive replay, durable queue, or Angular UI behavior
perform namespace alignment across the whole repository
add production hosting/security/live-ingestion/external adapter scope
close the milestone with a decision trace before discussion
```

## 9/10 Acceptance Criteria

Milestone 036 is successful when the following are true:

```text
Presentation references product API abstractions from Application, not a
  concrete Infrastructure API contract
product-facing API contract and response mapping are Application-owned
Infrastructure product services implement Application ports
project reference direction remains Domain <- Application <- Infrastructure
  and Presentation as composition root
architecture tests fail if Domain/Application depend on Infrastructure or
  Presentation namespaces
architecture tests fail if product HTTP endpoints take concrete Infrastructure
  product API contracts
major SRP hotspots have either a focused extraction or a documented bounded
  warning that does not block the 9/10 score
all accepted product, HTTP, CLI, persistence, and runtime tests still pass
```

## Change Log

### Change 1: Open Clean Architecture Hardening Milestone

Status: complete.

Intent:

```text
open a targeted architecture-hardening milestone with an explicit 9/10 goal
and a scoped implementation boundary
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/project-progress.md
docs/handoff.md
```

Verification:

```text
documentation-only opening change; runtime gate deferred until the detailed
implementation plan and code slices
```

### Change 2: Detailed Implementation Plan

Status: complete.

Intent:

```text
define behavior-preserving slices that move product API/use-case contracts
inward, add architecture guardrails, and reduce SRP hotspots toward the 9/10
architecture goal
```

Scope:

```text
docs/milestones/036-clean-architecture-hardening-plan.md
docs/milestones/036-clean-architecture-hardening.md
docs/handoff.md
```

Verification:

```text
documentation-only planning change; runtime gate deferred until slice 1
```
