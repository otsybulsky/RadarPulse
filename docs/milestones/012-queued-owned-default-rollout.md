# Milestone 012: Queued-Owned Default Rollout Architecture

Status: draft.

RadarPulse milestone 012 starts from the closed milestone 011
default-readiness evidence and defines the architecture for an explicit
provider-default rollout decision.

This document is intentionally not an implementation plan. It records the
default-rollout concept, compatibility posture, fallback model, rollout gate,
operator surface, validation posture, benchmark scope, and expected result
before any task breakdown is written.

Milestone 011 proved that the optimized
`queued-owned + pooled-copy + producer-consumer` contour is credible enough to
propose for a default rollout under measured limits. It did not change the
provider default. `blocking-borrowed` remains the current default provider and
same-run oracle.

The core decision is:

```text
011 proved queued-owned is credible enough to propose.
012 decides whether and how queued-owned becomes the default.
```

Milestone 012 should not broaden the runtime while changing the default. It
should either perform a controlled default switch with clear rollback posture,
or reject the switch with named blockers from the rollout gate.

## Milestone Goal

Milestone 012 should turn the milestone 011 default-readiness conclusion into
an explicit rollout decision for provider defaults.

The output of the milestone is the architectural definition of:

```text
provider-default switch decision and exact default contour
compatibility posture for existing CLI and benchmark usage
operator-selectable blocking-borrowed fallback
same-run borrowed reference availability for benchmark gates
rollout guardrails for correctness, cleanup, retained pressure, allocation,
  timing, and variance
configuration output that makes implicit defaults visible
failure behavior that does not silently hide unsafe retained ownership
Release gate matrix that supports or rejects the default change
decision trace that records the final default posture
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned or retained-owned input may enter the provider queue.
Retained resources release only after final use.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Controlled consumer delay remains mechanics-only proof.
Builder-transfer remains unsupported.
```

The key milestone boundary is:

```text
safe in 012:
  decide and implement a provider default change for the proven contour
  keep blocking-borrowed as an explicit fallback and oracle
  make default-expanded configuration visible in CLI and benchmark output
  repeat Release gates using natural, uncontrolled runs
  record a decision trace with guardrail thresholds and residual risks

not safe in 012 unless explicitly reprioritized:
  changing the queued-owned candidate contour while rolling it out
  silently falling back from queued-owned failure to borrowed success
  treating controlled consumer delay as default-readiness evidence
  adding builder-transfer, durable queues, live ingestion, cross-process
    transport, or concurrent rebalance execution
```

## Expected Outcome

At the end of milestone 012, RadarPulse should have a clear answer to this
question:

```text
Should queued-owned + pooled-copy + producer-consumer become the default
provider mode now, and under which rollback and validation guardrails?
```

The acceptable outcomes are:

```text
roll out:
  queued-owned becomes the default provider contour for the scoped commands,
  blocking-borrowed remains explicitly selectable, and Release gates pass
  under the recorded thresholds

defer:
  blocking-borrowed remains the default, queued-owned remains opt-in, and the
  decision trace names the blocker, threshold, or missing evidence
```

The expected result, if the rollout is accepted, is:

```text
default provider output expands to queued-owned + pooled-copy +
  producer-consumer where the scoped benchmark/runtime path uses defaults
explicit --provider blocking-borrowed remains available
same-run borrowed reference remains available for benchmark validation
effective configuration output makes default-derived settings reproducible
readiness/rollout gate passes correctness, topology/rebalance parity, cleanup,
  retained pressure, allocation ratio, timing, and variance checks
release failures stay at 0
current pending, active, and combined retained pressure returns to 0 at
  completion
residual allocation overhead is recorded against an explicit threshold
controlled proof rows remain excluded from natural rollout evidence
```

The core idea is:

```text
011 made the candidate defensible.
012 either makes the candidate the default deliberately, or preserves the old
default with a documented reason.
```

Milestone 012 is successful when default behavior is no longer ambiguous:
either the switch is completed with guardrails and fallback, or the switch is
rejected by a concrete gate result.

## Starting Position

Milestone 011 closed this default-candidate contour:

```text
provider mode:
  queued-owned

retained payload strategy:
  pooled-copy

provider overlap:
  producer-consumer

execution:
  async

workers:
  4 in the measured readiness contour

queue capacity:
  8

retained-byte budget:
  536_870_912

queue telemetry:
  summary

overlap telemetry:
  summary

overlap consumer delay:
  disabled for readiness gates
```

Milestone 011 evidence carried forward:

```text
correctness parity:
  passed

topology/rebalance parity:
  passed

release failures:
  0

expanded mixed-cache published base-data files:
  828

expanded mixed-cache payload values:
  32_306_203_200

expanded mixed-cache combined retained payload high-water:
  54_413_280 bytes

retained-byte budget:
  536_870_912 bytes

expanded mixed-cache borrowed elapsed ms:
  77_530.68

expanded mixed-cache candidate elapsed ms:
  71_181.17 after retained payload allocation optimization

post-optimization candidate allocation ratio:
  1.066x borrowed
```

Milestone 011 intentionally left these decisions for milestone 012:

```text
whether to change the provider default
which commands and runtime surfaces are in the first default rollout
how fallback to blocking-borrowed is selected and reported
which allocation ratio is acceptable for default behavior
which retained pressure threshold is acceptable for default behavior
which run variance threshold is acceptable for default behavior
how much same-run borrowed comparison remains required after the default switch
```

Milestone 011 intentionally deferred these runtime expansions:

```text
builder-transfer retained payload execution
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
durable queue or broker integration
live ingestion
cross-process provider or worker transport
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
```

Those deferrals remain out of scope unless milestone 012 explicitly changes
scope before implementation starts.

## Architectural Principles

Milestone 012 should follow these principles:

```text
defaults change only through an explicit decision trace
fallback is explicit operator choice, not silent success masking
borrowed remains available as validation oracle even after default rollout
effective configuration output must make default-derived settings visible
natural Release evidence decides rollout; controlled delay proves mechanics
retained pressure must stay bounded and return to zero at completion
release failures fail rollout
allocation overhead is acceptable only against a named threshold
performance is interpreted with repeated-run variance
the rollout should not change the candidate contour being evaluated
```

The milestone should separate these concerns:

```text
default contour:
  the provider settings used when users omit provider-specific flags

fallback:
  the explicit way to request blocking-borrowed behavior after the rollout

oracle:
  the borrowed reference used by benchmark gates and validation comparison

gate:
  the thresholded decision package that says whether the default can change

operator surface:
  CLI and result output that explains which settings came from defaults and
  which settings were user-selected

decision trace:
  the final record of what changed, why it changed, and how to roll it back
```

## Core Concepts

### Default Rollout Contour

The default rollout contour is the exact queued-owned shape that may become the
default provider behavior.

The rollout contour should start as the milestone 011 candidate:

```text
provider mode:
  queued-owned

retained payload strategy:
  pooled-copy

provider overlap:
  producer-consumer

queue capacity:
  8

retained-byte budget:
  536_870_912

queue telemetry:
  summary or the minimum level required for rollout gates

overlap telemetry:
  summary or the minimum level required for rollout gates

controlled consumer delay:
  disabled
```

The rollout should not tune these values opportunistically while collecting the
default-switch gate. If a value changes, the gate subject changes and the
decision trace must name that as a new contour.

The architecture should distinguish:

```text
current default before rollout:
  blocking-borrowed

candidate default after rollout:
  queued-owned + pooled-copy + producer-consumer

explicit fallback:
  blocking-borrowed selected by operator option

diagnostic alternatives:
  queued-owned variations that are not the default contour
```

### Compatibility Posture

The first default rollout should be conservative about compatibility.

Existing users must retain a clear way to request previous behavior:

```text
--provider blocking-borrowed
```

Benchmark and CLI output should not rely on operators remembering what the
defaults were in a given version. Output should print the expanded effective
configuration:

```text
provider mode
retained payload strategy
provider overlap mode
execution mode
worker count
queue capacity
retained-byte budget
queue telemetry level
overlap telemetry level
consumer-delay setting
evidence contour
evidence scope
```

The rollout may change omitted-option behavior, but it must not remove explicit
selection of the old provider path.

### Fallback And Oracle Model

`blocking-borrowed` has two roles after rollout:

```text
operator fallback:
  an explicit mode that preserves the old lifetime model when operators need
  to isolate queued-owned behavior

same-run oracle:
  the validation and benchmark reference used to compare default queued-owned
  results against the previous safe path
```

These roles must remain separate.

Fallback must not be silent:

```text
queued-owned retention failure must not be reported as a successful borrowed
  default run
processing failure must not transparently rerun through borrowed and claim
  queued-owned success
release failure must remain visible and fail rollout evidence
unsupported retained payload strategy must fail or be rejected explicitly
```

An explicit "run borrowed reference too" benchmark mode can continue to use
borrowed comparison. That is validation, not fallback.

### Rollout Gate

The rollout gate is stricter than an optimization benchmark. It decides default
behavior.

Candidate gate dimensions:

```text
correctness:
  deterministic same-run parity against blocking-borrowed for file counts,
  payload values, raw checksum, validation checksum, topology versions,
  accepted moves, skipped decisions, failed migrations, and validation status

cleanup:
  retained payload release failures stay at 0, accepted retained resources
  release exactly once, and current pending/active/combined pressure returns
  to 0 at completion

retained pressure:
  combined retained payload high-water remains within the configured budget
  and below the rollout threshold

allocation:
  candidate-to-borrowed allocation ratio remains at or below the accepted
  rollout threshold, with retained payload allocation reported separately

performance:
  candidate elapsed time is faster than or acceptably close to same-run
  borrowed after variance is considered

variance:
  repeated natural rows make spread visible before the default changes

configuration:
  output proves the run used the default rollout contour, not a diagnostic
  variant or controlled proof

evidence scope:
  controlled consumer-delay rows are excluded from natural rollout evidence
```

Initial threshold candidates for the implementation plan to confirm:

```text
release failures:
  must equal 0

validation parity:
  must pass

current retained pressure at completion:
  pending, active, and combined counts/bytes must return to 0

combined retained payload high-water:
  must stay within the configured retained-byte budget

allocation ratio:
  should explicitly decide whether the milestone 011 value of 1.066x borrowed
  is acceptable, and define the maximum allowed ratio before gate capture

performance delta:
  should define whether default requires faster-than-borrowed or allows a
  bounded slowdown in exchange for rollout value

variance:
  should define maximum acceptable spread before interpreting timing deltas
```

The milestone should not choose thresholds after seeing the gate result.

### Default Configuration Surface

The default configuration surface is where omitted provider options resolve to
the rollout contour.

Candidate surfaces to audit:

```text
CLI option parsing and defaults
archive rebalance benchmark defaults
cache benchmark defaults
provider mode enum defaults
retained payload strategy defaults
provider overlap defaults
queue capacity defaults
retained-byte budget defaults
queue and overlap telemetry defaults
tests that assert default output or option expansion
documentation and handoff text that names current defaults
```

The implementation plan should decide the exact rollout scope. The architecture
requires that scope to be explicit:

```text
in scope:
  the first command/runtime surfaces whose omitted provider flags should now
  resolve to queued-owned

out of scope:
  any command/runtime surface that should preserve blocking-borrowed by default
  until a later milestone
```

If the default changes only for benchmark/runtime paths, the CLI output must
make that scoped behavior clear.

### Operator Telemetry Contract

Operator telemetry should make the new default auditable.

Every scoped default run should expose:

```text
provider mode
whether provider mode came from default or explicit option
retained payload strategy
provider overlap mode
queue capacity
retained-byte budget
pending retained batch/payload current and high-water values
active retained batch/payload current and high-water values
combined retained batch/payload current and high-water values
provider blocked elapsed
consumer idle elapsed
release attempts, released batches, not-required releases, already-released
  releases, and failed releases
evidence contour
evidence scope
validation status and deterministic output summary
```

The telemetry should stay bounded. Default rollout should not introduce
unbounded per-batch diagnostic output.

### Failure Policy

Default behavior must fail closed for retained ownership problems.

Required policy:

```text
retention failure:
  stop intake, release accepted resources, and report candidate failure

queue acceptance failure:
  stop or reject deterministically without claiming successful default output

processing failure:
  fault the queued session, release accepted resources, and prevent later
  success claims

validation failure:
  report default-run validation failure, not hidden fallback success

release failure:
  increment failed-release telemetry and fail rollout evidence

cancellation:
  stop intake deterministically and release accepted retained resources
```

The default rollout should not introduce automatic borrowed fallback on any of
these paths unless a later milestone explicitly designs visible fallback
semantics.

### Natural Versus Controlled Evidence

Natural rollout evidence uses real replay and processing timing with controlled
consumer delay disabled.

Controlled proof remains useful for mechanical checks:

```text
queue-ahead behavior
retained-byte backpressure
cleanup under backlog
active retained-resource accounting
```

Controlled proof remains invalid for default rollout timing:

```text
controlled consumer delay rows must not satisfy the rollout gate
controlled rows must remain labeled controlled-mechanics-proof
natural default rows must remain labeled natural-readiness or rollout evidence
```

Milestone 012 should preserve the milestone 011 interpretation:

```text
natural queue depth 1 is not a blocker by itself.
Backlog is not required for default rollout when producer/consumer lifetime
overlap, bounded retained pressure, clean release health, and timing evidence
are favorable.
```

## Target Runtime Posture

If the rollout is accepted, the scoped default path becomes:

```text
archive replay producer
  -> builds callback-scoped RadarEventBatch input
  -> retains owned payload through pooled-copy
  -> enqueues retained-owned input into a bounded provider queue
  -> records pending retained-resource pressure
  -> continues while bounded queue and retained-byte limits allow

processing consumer
  -> dequeues retained-owned input in provider sequence order
  -> moves retained pressure from pending to active
  -> captures latest topology snapshot immediately before processing
  -> processes one rebalance-enabled batch at a time
  -> validates output and runs rebalance control-plane work
  -> releases retained resources after final use
  -> records active and combined retained pressure
```

The fallback path remains:

```text
archive provider callback
  -> supplies borrowed RadarEventBatch during callback lifetime only
  -> processing completes before callback returns
  -> no retained provider queue is used
```

Both paths should continue to support same-run comparison in benchmark gates.

## Validation

Validation remains borrowed-reference driven.

The rollout gate should compare default queued-owned output against a same-run
`blocking-borrowed` reference when practical.

Validation must preserve:

```text
published file count
payload value count
raw checksum
validation checksum
topology version count
rebalance evaluation count
accepted move count
skipped decision count
failed migration count
skipped reason counters
retained resource release counts
validation status and errors
```

The gate should distinguish:

```text
default failed correctness
default failed cleanup
default exceeded retained pressure threshold
default exceeded allocation threshold
default regressed performance beyond threshold
default evidence was too noisy
default passed and is safe to roll out under the scoped posture
```

## Benchmark Scope

Milestone 012 benchmark scope should focus on natural Release rollout evidence.

Required benchmark posture:

```text
Release build
same-run blocking-borrowed reference
default queued-owned rollout contour
controlled consumer delay disabled
retained pressure telemetry enabled
overlap telemetry enabled
deterministic output comparison captured
repeated rows captured clearly enough to show variance
expanded effective configuration printed
```

Candidate data contours:

```text
primary KTLX 2026-05-04 contour used by milestones 010 and 011
expanded mixed-cache contour over local data
at least one repeated matrix where elapsed spread can be interpreted
single-file or small-cache smoke for default option expansion
controlled-delay contour only as separate mechanics regression proof if needed
```

Benchmark interpretation should report:

```text
candidate/default elapsed time versus same-run borrowed
best, worst, and average elapsed time where repeated rows exist
candidate/default allocation ratio versus borrowed
retained payload allocation separately from end-to-end allocation
pending, active, and combined retained pressure high-water marks
retained pressure as a ratio of configured budget
provider blocked elapsed and consumer idle elapsed
queue depth and HasQueuedAheadOverlap
release attempts and failed releases
validation parity
effective configuration and whether provider settings came from defaults
```

## Documentation And Handoff

Milestone 012 documentation should remain explicit about the default posture.

Expected documents:

```text
docs/milestones/012-queued-owned-default-rollout.md
docs/milestones/012-queued-owned-default-rollout-plan.md
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
docs/milestones/012-queued-owned-default-rollout-decision-trace.md
docs/milestones/012-queued-owned-default-rollout-closeout.md
docs/handoff.md
```

The handoff should state one of:

```text
queued-owned is now the scoped default, with blocking-borrowed available as
  explicit fallback

or

blocking-borrowed remains the default because the rollout gate found a named
  blocker
```

## In Scope

Milestone 012 includes:

```text
provider default rollout decision
scoped default provider change if gate thresholds support it
explicit blocking-borrowed fallback selection
same-run borrowed reference preservation for benchmarks
default-expanded CLI and benchmark configuration output
rollout gate thresholds for correctness, cleanup, pressure, allocation,
  timing, and variance
natural Release gate matrix for the rollout contour
failure policy verification for default queued-owned behavior
decision trace and closeout
handoff update for the final default posture
```

## Out Of Scope

Milestone 012 does not implement:

```text
builder-transfer retained payload execution
durable broker integration
live ingestion
cross-process provider or worker transport
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failures to borrowed success
```

## Completion Criteria

Milestone 012 is complete when:

```text
the rollout contour and rollout scope are explicit
rollout gate thresholds are documented before final gate interpretation
default-derived provider configuration is visible in output
blocking-borrowed remains explicitly selectable
same-run borrowed comparison remains available for benchmark gates
natural Release rollout evidence is captured and interpreted
controlled proof remains separated from rollout evidence
failure and cleanup policy is tested for default queued-owned behavior
the decision trace records whether the provider default changed
the closeout records verification, gate results, residual risks, and fallback
handoff states the current default provider unambiguously
```

The milestone should close with a default decision, not with another implicit
readiness statement.

## Likely Next Milestone Input

If milestone 012 rolls out queued-owned as the scoped default, the next
milestone can focus on one of:

```text
post-rollout hardening and broader workload validation
operator documentation and compatibility cleanup
more detailed allocation attribution or residual allocation reduction
producer replay optimization
processing consumer latency reduction
```

If milestone 012 defers the default switch, the next milestone should target
the named blocker:

```text
allocation threshold miss
retained pressure threshold miss
release or cleanup failure
validation parity failure
performance or variance regression
configuration or operator-surface ambiguity
```

Still deferred unless explicitly reprioritized:

```text
durable queues
live ingestion
cross-process workers
concurrent rebalance processing
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```
