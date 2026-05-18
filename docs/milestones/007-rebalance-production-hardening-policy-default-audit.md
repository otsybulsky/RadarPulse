# Milestone 007 Rebalance Production Hardening Policy Default Audit

Status: accepted after slice 16.
Date: 2026-05-18.

This audit records the accepted default policy and benchmark settings for the
milestone 007 synchronous rebalance hardening work. The goal is to make sure the
new hardening controls do not quietly make the controller more aggressive,
disable useful diagnostics, or reintroduce unbounded telemetry retention.

## Decision

No code default changes are required before closeout.

The current defaults are accepted because they are conservative, bounded, and
observable:

- validation remains visible by default with `diagnostic`;
- telemetry detail retention is bounded by default with `recent`;
- quarantine retry and clearing require logical evidence rather than a single
  sample;
- synthetic pressure skew is disabled unless explicitly requested;
- benchmark defaults are smoke-safe, while release comparison commands remain
  explicit about iterations, topology, parallelism, retention, and skew.

Future tuning should be explicit and evidence-backed. In particular, a future
production-shaped runtime may choose `essential` validation, but milestone 007
benchmarks and tests should keep `diagnostic` unless a benchmark command
intentionally compares validation profiles.

## Hardening Defaults

| Surface | Accepted default | Source | Audit rationale |
| --- | --- | --- | --- |
| Hardening profile | default hardening uses default retention, default quarantine lifecycle, and `diagnostic` validation | `RadarProcessingRebalanceHardeningOptions.Default` | Preserves diagnostic visibility while keeping retention bounded. |
| Validation profile | `diagnostic` | `RadarProcessingRebalanceHardeningOptions` and benchmark CLI parsers | Keeps route, telemetry, pressure, decision, migration, and handoff checks visible in tests and closeout benchmarks. |
| Telemetry retention mode | `recent` | `RadarProcessingTelemetryRetentionOptions.Default` | Retains bounded recent detail for debugging without growing decision history forever. |
| Retained decisions | `128` | `RadarProcessingTelemetryRetentionOptions.Default` | Enough recent context for diagnosis; bounded for long archive runs. |
| Retained lifecycle transitions | `64` | `RadarProcessingTelemetryRetentionOptions.Default` | Captures quarantine behavior without retaining unbounded lifecycle history. |
| Retained accepted moves | `64` | `RadarProcessingTelemetryRetentionOptions.Default` | Captures move pressure context while keeping detail capped. |
| Retained validation failures | `32` | `RadarProcessingTelemetryRetentionOptions.Default` | Keeps recent failure examples while counters preserve totals. |
| Quarantine TTL | `64` logical evaluations | `RadarProcessingQuarantineLifecycleOptions.Default` | Avoids immediate retry storms; stale evidence still decays automatically. |
| Sustained cooling samples | `3` | `RadarProcessingQuarantineLifecycleOptions.Default` | Requires repeated cooling evidence, not one quiet sample. |
| Material pressure change | `0.25` | `RadarProcessingQuarantineLifecycleOptions.Default` | Avoids retrying on cosmetic pressure changes. |

## Rebalance Policy Defaults

The milestone 006 rebalance policy defaults are still accepted:

| Surface | Accepted default | Source | Audit rationale |
| --- | --- | --- | --- |
| Budget window | `1` evaluation | `RadarProcessingRebalanceOptions.Default` | Keeps budget accounting immediate and simple for the synchronous reference path. |
| Global move budget | `1` move per window | `RadarProcessingRebalanceOptions.Default` | Prevents multi-move churn from a single evaluation. |
| Source shard move budget | `1` move per window | `RadarProcessingRebalanceOptions.Default` | Prevents draining one hot shard too aggressively. |
| Target shard receive budget | `1` move per window | `RadarProcessingRebalanceOptions.Default` | Prevents overloading a cold target shard in one window. |
| Minimum partition residency | `3` evaluations | `RadarProcessingRebalanceOptions.Default` | Protects new ownership from immediate churn. |
| Partition move cooldown | `5` evaluations | `RadarProcessingRebalanceOptions.Default` | Keeps moved partitions stable across several logical evaluations. |
| Source shard move cooldown | `1` evaluation | `RadarProcessingRebalanceOptions.Default` | Avoids back-to-back source churn. |
| Target shard receive cooldown | `1` evaluation | `RadarProcessingRebalanceOptions.Default` | Avoids back-to-back target churn. |
| Minimum projected benefit | `0.05` | `RadarProcessingRebalanceOptions.Default` | Rejects cosmetic moves. |
| Target headroom threshold | `double.MaxValue` | `RadarProcessingRebalanceOptions.Default` | Keeps the milestone 006 behavior; stricter target limits require separate evidence. |

Milestone 007 hardening did not add evidence that these policy defaults should
become more aggressive. Pressure skew is available for stress contours, but it
is not a reason to change the baseline policy.

## Pressure Defaults

| Surface | Accepted default | Source | Audit rationale |
| --- | --- | --- | --- |
| Pressure scoring | event weight `1.0`, payload value weight `0.001`, raw checksum weight `0.0` | `RadarProcessingPressureOptions.Default` | Keeps pressure tied to work volume without using checksum as load. |
| Pressure windows | capacity `8`, minimum samples `3` | `RadarProcessingPressureWindowOptions.Default` | Requires repeated evidence before pressure classification drives movement. |
| Warm thresholds | exit `8,000`, enter `10,000` | `RadarProcessingPressureWindowOptions.Default` | Keeps hysteresis between normal and warm. |
| Hot thresholds | exit `40,000`, enter `50,000` | `RadarProcessingPressureWindowOptions.Default` | Keeps hysteresis before hot-shard relief. |
| Super-hot thresholds | exit `80,000`, enter `100,000` | `RadarProcessingPressureWindowOptions.Default` | Keeps severe pressure classification distinct. |

Synthetic workloads may override pressure windows to create deterministic
behavioral contours. Those overrides are workload definitions, not production
default changes.

## Benchmark CLI Defaults

These defaults are accepted for quick smoke runs. Release closeout comparisons
must continue to pass explicit settings so runs remain comparable.

| Command | Accepted defaults | Audit rationale |
| --- | --- | --- |
| `processing benchmark rebalance-synthetic` | workload `balanced`, modes `all`, validation `diagnostic`, no quarantine overrides, iterations `3`, warmup `1` | Exercises all synthetic contours for a small default smoke without hiding validation. |
| `processing benchmark rebalance-archive` | max files `20`, modes `all`, partitions `24`, shards `4`, iterations `1`, warmup `0`, parallelism `1`, decompressor default, validation `diagnostic`, retention default, quarantine default, skew `none` | Safe local smoke over real archive data; no synthetic skew unless explicitly requested. |
| `processing benchmark synthetic` | sequential, sources `16`, batches `4`, events per batch `1024`, payload values `4`, partitions `1`, shards `1`, handlers `none`, iterations `3`, warmup `1` | Preserves the original processing-only smoke shape. |

The archive rebalance default `parallelism 1` is intentionally conservative for
local smoke behavior. Milestone 006/007 performance comparisons use explicit
parallelism, typically `24`, when comparing against archive stream baselines.

## Pressure Skew Defaults

| Surface | Accepted default | Source | Audit rationale |
| --- | --- | --- | --- |
| Pressure skew profile | `none` | `RadarProcessingPressureSkewOptions.None` and archive CLI parser | Baseline real-data runs must observe real archive pressure without synthetic overlay. |
| Pressure skew factor | `1.0` | `RadarProcessingPressureSkewOptions.None` | Inert while profile is `none`; meaningful for explicit skew contours. |
| Pressure skew period | `8` | `RadarProcessingPressureSkewOptions.None` | Inert while profile is `none`; used only by explicit rotating skew profiles. |

Skewed runs should be reported as "real archive with synthetic pressure
overlay." They are useful for active rebalance stress, but they are not baseline
performance evidence.

## Evidence

The accepted defaults are backed by existing milestone 007 coverage and smoke
runs:

- `RadarProcessingRebalanceHardeningOptionsTests` covers hardening defaults,
  stable validation and retention enum values, invalid retention limits,
  invalid quarantine lifecycle values, and retention/profile independence.
- `RadarProcessingQuarantineLifecycleEvaluatorTests` covers TTL retry,
  sustained cooling, material pressure-change retry, repeated hot evidence, and
  invalid lifecycle settings.
- `RadarProcessingSyntheticRebalanceWorkloadTests` covers quarantine TTL retry,
  sustained cooling clear, pressure-change retry, retry re-entry, successful
  relief clear, and retention stress workloads.
- `RadarProcessingSyntheticRebalanceBenchmarkTests` and
  `RadarPulseCliRebalanceBenchmarkTests` cover validation profile wiring,
  retention preservation, pressure skew parsing, quarantine lifecycle CLI
  overrides, invalid hardening options, and CLI smoke behavior.
- Slice 16 verification passed: focused CLI/synthetic/allocation coverage
  `30 passed`; processing/presentation coverage `338 passed`; full suite
  `481 passed, 3 skipped`; Release build succeeded with `0 warnings` and
  `0 errors`.
- Full-cache archive retention and pressure-skew smokes in the handoff show
  bounded retention, visible skipped-reason counters, successful validation,
  and active rebalance stress when skew is explicitly enabled.

## Closeout Requirements

The final milestone 007 closeout should use these defaults as the accepted
baseline policy unless a later benchmark proves a change is needed. The closeout
must still include explicit comparison runs for:

- default hardening with no pressure skew;
- counters-only retention where allocation/retention cost matters;
- validation profile comparisons where validation cost is being measured;
- explicit pressure skew stress contours, clearly separated from baseline
  real-data runs.
