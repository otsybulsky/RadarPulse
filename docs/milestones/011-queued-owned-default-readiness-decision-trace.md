# Milestone 011: Decision Trace

Status: complete.

Closeout: `011-queued-owned-default-readiness-closeout.md`.

Performance gate:
`011-queued-owned-default-readiness-performance-gate.md`.

## 1. What Was Implemented

Milestone 011 turned the optimized milestone 010
`queued-owned + pooled-copy + producer-consumer` contour into a measured
future-default candidate while keeping `blocking-borrowed` as the current
default and same-run oracle.

Implemented:

- Baseline readiness audit and frozen default-candidate contour.
- Pending, active, and combined retained-resource pressure contracts.
- Active consumer retained-resource accounting after dequeue.
- Provider queue and overlap telemetry propagation for pending, active, and
  combined retained pressure.
- Archive benchmark and CLI output for candidate configuration, queue pressure,
  overlap pressure, release health, allocation, and evidence labels.
- Readiness evaluator contracts for correctness, topology/rebalance parity,
  release health, pressure budget, allocation movement, performance delta, run
  variance, effective configuration, and natural evidence.
- Failure, cancellation, and cleanup coverage for retained-resource lifecycle
  paths.
- Natural Release gate matrix with same-run borrowed reference rows.
- Expanded-cache follow-up covering multiple local radar/date shapes.
- Retained payload allocation optimization that reduced candidate excess
  allocation by `87.91%` against the expanded-cache borrowed reference.
- Controlled-proof separation hardening in CLI labels and tests.

The milestone does not change the provider default.

## 2. Decision Matrix

### Treat Queued-Owned As A Future Default-Rollout Candidate

Decision: accept `queued-owned + pooled-copy + producer-consumer` as a credible
future default-rollout candidate.

Why chosen: the final evidence set has correctness parity, topology/rebalance
parity, clean retained-resource release health, bounded pending-plus-active
retained pressure, favorable local timing, closed local input-diversity
follow-up, and a resolved retained-payload allocation regression.

Alternatives: reject the contour outright, keep it only as a diagnostic mode,
or switch it to default inside milestone 011.

Rejected because: outright rejection ignores the measured improvements and
cleanup health; diagnostic-only status understates the now-complete readiness
evidence; switching inside this milestone would bypass an explicit rollout
decision and rollback posture.

Trade-offs/debt: the rollout candidate still has a residual allocation overhead
of `1.066x` borrowed on the expanded mixed-cache contour. The next milestone
must decide whether that overhead is acceptable for default behavior and should
preserve a clear fallback to `blocking-borrowed`.

Review explanation: "011 makes queued-owned default-ready enough to propose;
it does not make it the default."

### Keep Blocking Borrowed As The Current Default

Decision: keep `blocking-borrowed` as the current provider default and same-run
correctness oracle at milestone closeout.

Why chosen: the borrowed path is still the simplest lifetime model and the
stable reference used to validate queued-owned correctness, timing, and
allocation movement.

Alternatives: silently switch the default, make default selection depend on
execution mode, or remove the borrowed path from benchmark comparison.

Rejected because: default behavior should change only through an explicit
rollout milestone. Execution-mode coupling would hide provider semantics, and
removing borrowed comparison would weaken future readiness gates.

Trade-offs/debt: operators must still opt into the queued-owned candidate until
the default-rollout milestone completes.

Review explanation: "Borrowed remains the oracle until rollout explicitly
replaces it."

### Accept Natural Queue Depth 1 As Balanced Pipeline Evidence

Decision: do not require natural queue depth greater than `1` as a
default-readiness blocker.

Why chosen: controlled consumer-delay runs already proved queued-ahead mechanics
when the consumer is intentionally slowed. Natural Release rows with queue depth
`1` showed producer/consumer lifetime overlap, clean release health, bounded
retained pressure, and favorable timing without backlog accumulation.

Alternatives: require natural queued-ahead overlap, increase artificial pressure
until depth grows, or treat `HasQueuedAheadOverlap = no` as a failure.

Rejected because: forcing backlog would optimize for a symptom instead of
production behavior. On the measured natural contours, the consumer kept up and
retained pressure stayed bounded, which is favorable.

Trade-offs/debt: future workloads may still produce natural backlog. The
rollout milestone should continue to record queue depth and retained pressure
so backlog remains visible when it happens.

Review explanation: "Controlled delay proves the queue can run ahead; natural
depth 1 says this workload did not need to."

### Treat Controlled Delay As Mechanics Proof Only

Decision: controlled consumer-delay rows remain mechanics proof and cannot
satisfy natural readiness.

Why chosen: synthetic delay proves bounded queue-ahead behavior, retained-byte
backpressure, active pressure accounting, and cleanup under backlog, but it is
not production throughput evidence.

Alternatives: include controlled rows in natural performance aggregates or hide
the controlled contour behind the same default-candidate label.

Rejected because: mixing controlled and natural rows would overstate
production-readiness evidence.

Trade-offs/debt: documentation and CLI output carry more labels, but the
operator-facing distinction is explicit:

```text
natural-default-candidate -> natural-readiness
controlled-proof -> controlled-mechanics-proof
natural-opt-in -> opt-in-diagnostic
not-applicable -> not-applicable
```

Review explanation: "Controlled proof is allowed to prove mechanics, not
default performance."

### Accept Retained Payload Pooling With Bounded Idle Arrays

Decision: use a bounded large byte-array pool in the default retained payload
factory.

Why chosen: the natural gate exposed retained payload allocation as the main
remaining blocker. The bounded pool reduced expanded mixed-cache retained
payload allocation from `2_084_784_408` bytes to `247_679_944` bytes and
end-to-end candidate allocation from `5_897_703_080` bytes to
`4_063_709_976` bytes.

Alternatives: keep `ArrayPool<byte>.Shared`, raise readiness allocation
tolerance without optimization, or pursue `builder-transfer` first.

Rejected because: shared array pooling still caused high churn for large
payloads; raising tolerance would hide the regression; builder transfer needs
stronger ownership-transfer semantics and remains out of scope.

Trade-offs/debt: the idle large-array pool retains up to 4 arrays and 128 MiB by
default. That idle reuse budget is separate from live pending/active retained
pressure and must remain bounded.

Review explanation: "Optimize allocation churn without pretending idle reuse is
live queue pressure."

### Carry Residual Allocation As A Caution, Not A Blocker

Decision: record the remaining `1.066x` candidate-to-borrowed allocation ratio
as a rollout caution, not as a milestone blocker.

Why chosen: the excess allocation dropped by `87.91%`, retained payload
allocation dropped by `88.12%`, and candidate end-to-end allocation dropped by
`31.10%` while preserving validation, release health, and pressure bounds.

Alternatives: block closeout until exact allocation parity, or ignore the
remaining overhead.

Rejected because: exact parity is unlikely for an owned queued path that
retains input past callback lifetime, while ignoring the remaining overhead
would weaken rollout risk tracking.

Trade-offs/debt: the rollout milestone should keep allocation movement in the
gate and decide an explicit acceptable threshold.

Review explanation: "The allocation blocker is solved; the allocation caution
is still real."

### Defer Builder Transfer And Concurrent Rebalance

Decision: keep `builder-transfer`, multi-batch rebalance concurrency, ordered
concurrent commit barriers, durable queues, live ingestion, and cross-process
transport out of milestone 011.

Why chosen: milestone 011 is an evidence and readiness milestone for the
existing optimized contour, not a runtime expansion.

Alternatives: use the closeout to expand scope into lower-level transfer or
concurrency work.

Rejected because: those changes would alter the evidence subject after the gate
had been captured and would require new architecture.

Trade-offs/debt: future performance work may revisit builder transfer or
concurrent rebalance, but that should not be bundled with default rollout.

Review explanation: "Roll out the proven contour before changing the contour."

## 3. Evidence Summary

Natural gate support:

```text
correctness parity: passed
topology/rebalance parity: passed
release failures: 0
combined retained payload high-water on expanded cache: 54_413_280 bytes
retained-byte budget: 536_870_912 bytes
expanded cache published files: 828
expanded cache payload values: 32_306_203_200
expanded cache validation checksum: 615_051_108_812_661_629
expanded local shapes: 2026-05-04/KINX, 2026-05-04/KTLX, 2026-05-05/KTLX
```

Timing and allocation:

```text
expanded borrowed elapsed ms: 77_530.68
expanded pre-optimization candidate elapsed ms: 72_440.28
expanded post-optimization candidate elapsed ms: 71_181.17
borrowed allocated bytes: 3_811_549_280
pre-optimization candidate allocated bytes: 5_897_703_080
post-optimization candidate allocated bytes: 4_063_709_976
post-optimization candidate allocation ratio: 1.066x borrowed
```

Natural overlap interpretation:

```text
producer-consumer overlap: yes
queued-ahead overlap: no
queue depth high watermark: 1
interpretation: balanced natural pipeline behavior, not a failed mechanics
  proof
```

## 4. Final Decision

Milestone 011 concludes that the optimized queued-owned contour is ready to be
proposed for an explicit default-rollout milestone under measured limits.

The milestone does not switch the default provider. The next milestone should
decide rollout mechanics, guardrails, fallback behavior, and any final Release
gate threshold before changing default behavior.
