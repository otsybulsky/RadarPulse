# Milestone 036 Decision Trace

Date: 2026-05-28

Decision: accept clean architecture hardening toward a defensible 10/10
posture over the accepted local product demo/runtime boundary with named
scoped warnings.

This decision accepts milestone 036's Application-owned product API boundary,
focused Application product ports, Presentation-to-Application dependency
direction, Infrastructure adapter implementations, executable architecture
guardrails, product orchestration SRP cleanup, CLI entrypoint and command
family extractions, Domain friend assembly removal, processing benchmark
stabilization, full-cache and processing-only performance evidence, large
production class SRP treatment, oversized test fixture SRP sweep, final
physical file validation, Release build, full Release test suite, and handoff
update on top of the accepted local product demo/readiness architecture.

The accepted scope is architecture hardening, not a product behavior
expansion. RadarPulse keeps the accepted product API, HTTP, CLI, persistence,
runtime defaults, local history, demo/readiness workflows, and deterministic
archive-shaped input posture. The milestone improves where dependencies live,
how responsibilities are assigned, which boundaries are guarded by tests, and
how large orchestration/state-machine surfaces are physically organized.

The decision deliberately does not claim true live network ingestion, public
production deployment, authentication, authorization, TLS termination,
production CORS hardening, external broker/cloud queue/database adapter
readiness, database-backed product history, cross-machine throughput
certification, exactly-once production delivery, or fastest-in-the-world
performance. The accepted performance evidence is local Release evidence and
supports a restrained source-routed radar event processing claim only inside
the documented workload boundary.

## Decision Matrix

```text
clean architecture hardening toward 10/10:
  accepted with scoped warnings

Application-owned product API boundary:
  accepted; product-facing service and API contracts are owned by Application
  instead of Presentation depending on concrete Infrastructure API classes

Presentation dependency direction:
  accepted; product HTTP endpoints depend on Application product API
  abstractions

Infrastructure adapter posture:
  accepted; Infrastructure implements Application product ports and remains
  behind composition/adapter wiring

focused Application product ports:
  accepted; run, query, history, and control responsibilities are exposed as
  focused ports for the product API contract

compatibility aggregate:
  accepted with boundary; IRadarPulseProductPipelineService may remain as a
  compatibility aggregate, but Product API composition is guarded against
  regressing to one broad Presentation-facing dependency

architecture guardrails:
  accepted; tests guard project direction, namespace direction, Product API
  ownership, Product API port segregation, Domain friend access, HTTP endpoint
  API dependency, and thin Program.cs entrypoint shape

product orchestration SRP cleanup:
  accepted; synthetic batch creation, handler-set creation, and archive batch
  capture moved into focused collaborators

CLI entrypoint and command-family SRP cleanup:
  accepted; Program.cs is a thin entrypoint and RadarPulseCliApplication is a
  top-level router over focused archive, processing, product, usage,
  formatting, and option responsibilities

Domain friend assembly removal:
  accepted; Domain no longer grants InternalsVisibleTo access to
  Infrastructure

large production class SRP treatment:
  accepted; benchmark, queue, durable, and session surfaces were split through
  focused collaborators or responsibility-named partial folders where class
  identity intentionally remains intact

oversized test fixture SRP sweep:
  accepted; large test fixtures were split into dedicated per-class folders
  and responsibility-named partial files

physical file-size guardrail:
  accepted; src/tests C# inventory has 0 files above 250 code-ish lines and
  the current maximum is 249

processing benchmark stabilization:
  accepted; order-sensitive allocation assertions were replaced or bounded by
  deterministic run-local evidence where appropriate

full-cache performance evidence:
  accepted; full-cache custom-handler rows remain correctness-clean at about
  440K-448K RadarStreamEvent/s and about 522M-531M payload values/s

processing-only technology evidence:
  accepted with claim boundary; processing-only heavy-handler rows remain
  above 2.0M RadarStreamEvent/s in sequential and partitioned modes and above
  1.14M RadarStreamEvent/s through async worker transport

active=4 heavy-handler allocation:
  accepted as performance debt; it is not a Clean Architecture, GRASP, SOLID,
  or GoF blocker for this milestone

final architecture score:
  accepted as 10/10 for Clean Architecture, GRASP, SOLID, pragmatic GoF,
  automated guardrails, and evidence posture within the accepted milestone
  scope
```

## Decision Explanations

### Accept Application-Owned Product Boundary

Decision: accept Application-owned product service/API contracts and make
Presentation consume the Application product API abstraction.

Why chosen: the pre-milestone shape let product-facing Presentation code sit
too close to concrete Infrastructure API classes. Moving the product contract
inward makes the use-case vocabulary Application-owned and keeps
Infrastructure in an adapter role.

Alternatives: keep the concrete Infrastructure product API contract, add a
Presentation-specific wrapper, or move only DI registration without moving the
contract ownership.

Rejected because: the concrete Infrastructure dependency was the main Clean
Architecture issue; a Presentation wrapper would hide rather than fix the
direction; DI-only changes would not make the contract boundary durable.

Trade-offs/debt: Application now owns more product-facing contract surface.
That is intentional for use-case vocabulary, but future product expansion
should keep DTO/API growth restrained.

Review explanation: "Product HTTP code now talks inward to Application
contracts, while Infrastructure remains an implementation detail."

### Accept Focused Product Ports

Decision: accept focused run, query, history, and control product ports while
retaining the broad service interface only as a compatibility aggregate.

Why chosen: the Application product API contract should not depend on one
large service dependency when it only needs focused capabilities. Splitting
the ports directly addresses the Interface Segregation warning.

Alternatives: delete the broad aggregate immediately, keep all consumers on
one broad interface, or split ports only in Infrastructure.

Rejected because: immediate aggregate removal would add unnecessary churn to
tests and composition; keeping one broad interface preserves the ISP smell;
Infrastructure-only splitting would not improve the Application-facing
contract.

Trade-offs/debt: the compatibility aggregate still exists. It is accepted
because the guarded Presentation/API boundary uses focused ports and the
aggregate can be removed later if it stops serving compatibility.

Review explanation: "The public product API contract depends on the
capabilities it needs, not on a grab-bag service."

### Accept Executable Architecture Guardrails

Decision: accept architecture tests as part of the milestone definition of
done.

Why chosen: the target score depends on boundaries staying fixed after the
refactor. Project-reference, namespace, Product API ownership, endpoint
dependency, Domain friend access, port segregation, and thin-entrypoint tests
make the intended architecture executable.

Alternatives: rely on code review, document the rules only, or add a
third-party architecture-testing framework.

Rejected because: review-only rules regress easily; documentation-only rules
do not fail builds; the current source/reflection tests are enough for the
local guardrail surface without adding another dependency.

Trade-offs/debt: source/reflection guardrails are narrower than a complete
static architecture analysis tool. They cover the accepted risks for this
milestone.

Review explanation: "The architecture boundary is now backed by tests that
fail when the known bad directions return."

### Accept SRP Cleanup And Partial-Folder Rule

Decision: accept focused collaborator extraction first, and accept partial
class folders only for surfaces whose class identity intentionally remains
intact.

Why chosen: some large orchestration, benchmark, queue, durable, and test
fixture surfaces had real SRP pressure. Where behavior-preserving extraction
was clear, responsibilities moved into collaborators. Where a compatibility
or state-machine owner needed to stay cohesive, responsibility-named partial
files made the physical file readable without scattering invariants.

Alternatives: leave large files unchanged, force all large classes into new
collaborators, or split files without a dedicated per-class folder rule.

Rejected because: unchanged large files left avoidable maintainability debt;
forcing every split into collaborators would risk state-machine correctness
and behavioral churn; unstructured partial files would make navigation worse.

Trade-offs/debt: some logical partial classes still represent large concepts.
The accepted guardrail is physical readability and responsibility naming:
src/tests now has no C# file above 250 code-ish lines.

Review explanation: "The codebase is easier to navigate without pretending
that every state-machine or compatibility surface can be safely decomposed in
one milestone."

### Accept Domain Friend Assembly Removal

Decision: accept removal of Domain InternalsVisibleTo access for
Infrastructure.

Why chosen: privileged Infrastructure access to Domain internals weakens the
layer boundary. Removing the friend assembly makes Infrastructure use public
Domain contracts or Domain-owned operations.

Alternatives: keep the friend assembly as a bounded warning, expose larger
public Domain surface quickly, or move affected behavior into
Infrastructure.

Rejected because: the friend assembly was a direct Clean Architecture
blocker; broad public exposure would overcorrect; moving domain behavior into
Infrastructure would invert the intended ownership.

Trade-offs/debt: public Domain APIs now need to be kept honest and compact.
Architecture guardrails prevent the friend assembly from returning silently.

Review explanation: "Infrastructure no longer has privileged backdoor access
to Domain internals."

### Accept Benchmark Stabilization And Performance Evidence

Decision: accept benchmark allocation stabilization plus separate full-cache
and processing-only evidence.

Why chosen: architecture closeout needed a full Release suite that does not
fail because unrelated tests ran first, and performance claims needed clear
separation between archive ingestion and processing-core handler throughput.

Alternatives: loosen allocation tests broadly, remove performance gates, or
use only full-cache end-to-end numbers for all claims.

Rejected because: broad loosening would hide regressions; removing gates
would weaken evidence posture; full-cache numbers measure file replay,
decompression, scanning, identity normalization, batch construction,
routing, and handlers together, so they cannot stand alone as a processing
engine claim.

Trade-offs/debt: full-cache active=4 heavy-handler allocation remains a
visible optimization target. It is accepted as performance debt because speed
and correctness are clean and the debt is documented with exact rows.

Review explanation: "The full suite is stable, and performance evidence now
separates end-to-end archive cost from handler-engine throughput."

### Accept 10/10 Architecture Verdict

Decision: accept 10/10 for Clean Architecture, GRASP, SOLID, pragmatic GoF,
automated guardrails, and evidence posture within the accepted milestone
scope.

Why chosen: the material blockers identified in the review were either
removed, guarded, or converted into explicitly bounded residual notes:
dependency direction is fixed, Application owns product contracts, focused
ports address ISP, Domain friend access is gone, SRP hotspots are reduced or
bounded, and tests/performance evidence support the final posture.

Alternatives: keep the score at 9/10 until compatibility aggregates and all
logical large partial classes disappear, or expand the milestone into
production deployment/security/live-ingestion work.

Rejected because: the remaining aggregate and logical large concepts are not
material blockers for the accepted local product demo/runtime boundary after
guardrails and physical SRP treatment; production expansion is outside this
milestone and would reopen accepted scope.

Trade-offs/debt: 10/10 is a practical engineering verdict for this milestone
scope, not a claim of universal perfection. Future production-grade
deployment, security, live ingestion, external adapters, or comparative
benchmark certification would require separate decisions and gates.

Review explanation: "For the accepted local product architecture boundary,
the major clean architecture, GRASP, SOLID, and pragmatic GoF concerns have
been addressed to the point where no material score blocker remains."

## Accepted Evidence

Milestone evidence is captured in:

```text
docs/milestones/036-clean-architecture-hardening.md
docs/milestones/036-clean-architecture-hardening-plan.md
docs/milestones/036-clean-architecture-hardening-performance-evidence.md
```

Verification accepted:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
    /p:UseSharedCompilation=false
  result: passed, 0 warnings, 0 errors

focused architecture/product/CLI Release gate:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter
    "FullyQualifiedName~Architecture|FullyQualifiedName~Product|FullyQualifiedName~RadarPulseCli|FullyQualifiedName~ProductPipelineCli"
    -c Release --no-build
  result: passed, 126 passed, 0 failed, 0 skipped

full Release test suite:
  dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release
    --no-build
  result: passed, 1016 passed, 0 failed, 3 skipped

src/tests C# file inventory:
  result: 0 files above 250 code-ish lines
  current maximum: 249 code-ish lines

git diff --check:
  result: passed
```

Performance evidence accepted:

```text
full-cache raw logs:
  data/perf/m036-full-cache-20260528-142529

processing-only raw logs:
  data/perf/m036-world-class-20260528-151123

full-cache counter-checksum-heavy active=4:
  447_152.29 RadarStreamEvent/s end-to-end
  530_028_245.90 payload values/s end-to-end
  12_212_257_456 allocated bytes
  448.08 bytes per RadarStreamEvent

processing-only counter-checksum-heavy:
  sequential: 2_101_506.66 RadarStreamEvent/s
  partitioned: 2_060_612.64 RadarStreamEvent/s
  async: 1_140_818.38 RadarStreamEvent/s
```

## Residual Risks And Limits

```text
IRadarPulseProductPipelineService remains as a compatibility aggregate, but
  Presentation-facing product API composition depends on focused Application
  ports and is guarded against regression.

Some logical partial classes still represent large adapter, benchmark,
session, compatibility, or test-fixture concepts. This is accepted only
  where class identity intentionally remains intact and files are split into
  bounded responsibility-named zones.

Full-cache active=4 heavy-handler delta/merge allocation remains the main
  performance debt. It is documented as optimization work, not an
  architecture score blocker.

The processing-only benchmark supports a restrained local technology claim,
  not external comparative certification. Public comparative claims require
  repeated runs, machine specification, CPU/GC telemetry, runtime version, and
  competitor-equivalent workload definitions.

No production deployment, security, live network ingestion, external adapter,
  database-backed history, or exactly-once delivery posture is accepted by
  this decision.
```

## Final Decision

```text
accepted with scoped warnings for clean architecture hardening toward a
defensible 10/10 posture over the accepted local product demo/runtime
boundary
```

Closeout status:

```text
decision trace written
closeout written:
  docs/milestones/036-clean-architecture-hardening-closeout.md
```
