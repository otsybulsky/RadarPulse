# Glossary

Status: active under milestone 037.

This glossary defines RadarPulse-specific terms as they are used in the
current handbook and milestone documents.

## A

Accepted boundary:
The explicit scope a milestone has proven and accepted. In the current
project, the strongest accepted boundary is the local deterministic product
demo/runtime boundary, not public production deployment.

Application:
The layer that owns product-facing contracts, ports, request/response models,
read models, and use-case vocabulary. Infrastructure implements Application
ports; Presentation consumes them.

Archive-shaped workload:
A deterministic workload shaped like historical/NEXRAD archive input. It is
used to prove runtime, product, and demo behavior without depending on live
network ingestion.

## B

BFF:
Backend-for-frontend. In RadarPulse this means product-facing read/query
models and API shapes intended for UI consumption without leaking low-level
runtime internals.

Batch:
A grouped set of radar stream events processed together. RadarPulse validates
batch shape, lifetime, metrics, payload size, checksums, and ordering-related
metadata before runtime paths consume it.

Broker-neutral durable envelope:
A durable processing wrapper that models claim, retry, recovery, poison, and
ordered commit semantics without tying the domain contract to one production
broker or cloud queue.

## C

Clean architecture boundary:
The accepted dependency direction: Domain is pure, Application owns ports and
product vocabulary, Infrastructure implements adapters, and Presentation
composes/exposes workflows.

Commit:
The point where internally computed work becomes externally visible state.
For ordered runtime paths, commit must follow provider sequence.

## D

Deterministic demo:
A local product workflow whose input, output, history, and verification are
repeatable enough for portfolio review and maintainer checks.

Diagnostics:
Operator/developer-facing explanation of status, warnings, validation
failures, retained pressure, release health, worker health, capacity, first
blocking reason, and readiness state.

Domain:
The layer that owns pure rules, models, validation, policies, and
deterministic services. It must not depend on Infrastructure, Presentation,
HTTP, CLI, Angular, or filesystem adapters.

Durable adapter:
The concrete implementation that stores and reloads durable envelopes. The
current accepted work proves a local persistent adapter shape, not production
broker/cloud certification.

Durable envelope:
The record that carries a processing batch through durable ownership,
claim/retry, recovery, ordered commit, and failure visibility.

## E

Evidence:
Tests, build results, package verification, performance matrices, closeouts,
and decision traces that justify an accepted milestone posture.

Exactly-once:
A production delivery claim requiring storage/downstream idempotency and
failure-mode proof beyond the current local demo boundary. RadarPulse does
not claim exactly-once end-to-end production delivery.

## F

Fail closed:
Reject or block unsafe/unsupported work with clear diagnostics instead of
silently producing ambiguous or unsafe output.

First blocking reason:
The earliest diagnostic reason explaining why readiness, merge, processing,
or release progress is blocked.

Freeze mode:
The project posture after a milestone closes when no feature/runtime
milestone is active by default. Documentation or targeted maintenance can
still be opened explicitly.

## H

Handler:
A processing extension that computes source-local or batch-local output.
Handlers may be handler-free, mergeable, snapshot-only, or unsupported for a
given runtime posture.

Handler delta:
An immutable per-batch representation of mergeable handler output. Deltas are
identified and merged by deterministic metadata and provider sequence.

Handler posture:
The accepted execution class for a handler set:

```text
handler-free:
  no handler state/output is involved

mergeable:
  explicitly supports deterministic per-batch delta compute and ordered merge

snapshot-only:
  keeps sequential fallback and committed snapshot export

unsupported:
  fails closed with diagnostics
```

## L

Local product demo:
The accepted same-machine product workflow using package scripts, local
RadarPulse.Http, built Angular Operator UI, deterministic demo/archive-shaped
input, and local file-backed history.

## M

Milestone:
A scoped unit of architecture, implementation, evidence, and closeout. The
handbook explains current state; milestone documents preserve the decision
history.

## O

Operator UI:
The Angular app under `src/Presentation/OperatorUi` that inspects readiness,
runs, batches, sources, handler output, diagnostics, capacity, controls, and
history through the local product API.

Ordered commit:
The rule that externally visible processing output is applied in provider
sequence, even if concurrent workers finish in a different order.

## P

Persistent run history:
The local file-backed product history used by the demo to preserve product
run summaries across host recreation. It is not a production database-backed
history service.

Port:
An Application-owned interface that describes what the application needs from
an outer adapter. Infrastructure implements ports; Presentation consumes the
application-facing shape.

Presentation:
The layer that exposes CLI, HTTP, and UI delivery and wires concrete
implementations as a composition root.

Product API:
The Application-owned request/response and use-case surface consumed by HTTP,
CLI, and UI product workflows.

Provider sequence:
The deterministic sequence number that represents the accepted order of
batches from a provider. It is the ordering basis for externally visible
commit and diagnostics.

## Q

Queued-owned provider:
The accepted provider posture where queued batches and retained payload
resources have explicit ownership, backpressure, release, readiness, and
diagnostic behavior.

## R

Radar event batch:
A validated batch of radar stream events with identity, lifetime, metrics,
payload, and checksum information.

Read model:
A product-facing projection of runtime state. Read models are stable enough
for UI/API consumption and should not expose low-level queue/session internals.

Readiness:
The product/operator answer to whether the current system state is displayable
or runnable, and if not, why not.

Rebalance:
The processing behavior that moves partition ownership to relieve hot/cold
pressure while preserving validation, migration, handoff, and ordered commit
rules.

Retained payload:
Payload resources held across queueing/processing boundaries. Retained
payloads must be owned, released, and diagnosed so pressure and leaks remain
visible.

## S

Same-origin delivery:
The local delivery mode where RadarPulse.Http serves both product API routes
and the built Angular Operator UI from one local origin.

Snapshot-only handler:
A stateful handler that can export committed snapshots but does not have an
accepted concurrent delta/merge contract. It uses sequential fallback.

Source universe:
The set of known radar sources and their stable mappings used by stream
identity and processing flows.

Streaming:
The domain area that normalizes radar stream identities, manages source
universe/dictionary state, and builds validated radar event batches.

## T

True live ingestion:
A real network radar ingestion posture. It is outside the accepted local
demo/runtime boundary.

## V

Verification gate:
A command or test set used to prove a behavior after a change. Examples
include focused xUnit filters, Release build, Angular unit/build gates,
browser smoke tests, and package `verify`.
