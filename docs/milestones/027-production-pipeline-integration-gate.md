# Milestone 027: Production Pipeline Integration Gate

Date: 2026-05-26

Status: captured before decision trace.

## Gate Scope

This gate validates the deterministic archive-shaped production pipeline
integration surface implemented for milestone 027.

Included:

```text
production pipeline profile and configuration resolution
operator summary and readiness contract
archive-shaped production pipeline runner
durable file-backed restart/recovery pipeline gate
rollback/fallback control diagnostics
handler output and BFF read-model compatibility
representative capacity evidence row
```

Excluded:

```text
Kafka, RabbitMQ, cloud queue, or database-backed adapter
production broker operations or retention certification
true live network ingestion
production HTTP BFF host
frontend application
deployment platform automation, autoscaling, alerts, and runbooks
cross-machine throughput certification
exactly-once production delivery claims
```

## Focused Release Gate

Command:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingProductionPipelineConfigurationTests|FullyQualifiedName~RadarProcessingProductionPipelineSummaryTests|FullyQualifiedName~RadarProcessingProductionPipelineRunnerTests|FullyQualifiedName~RadarProcessingProductionPipelineRecoveryTests|FullyQualifiedName~RadarProcessingProductionPipelineFallbackTests|FullyQualifiedName~RadarProcessingProductionPipelineGateTests"
```

Result:

```text
29 passed, 0 failed, 0 skipped
```

Coverage summary:

```text
production pipeline configuration:
  passed

operator summary/readiness:
  passed

archive-shaped pipeline runner:
  passed

durable restart/recovery:
  passed

rollback/fallback diagnostics:
  passed

handler output/BFF compatibility:
  passed

capacity evidence:
  passed
```

## Release Build

Command:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
succeeded, 0 warnings, 0 errors
```

## Accepted Evidence Before Decision Trace

Configuration profile:

```text
default production pipeline profile resolves queued-owned,
producer-consumer, pooled-copy, async shard transport, worker count 4,
worker queue capacity 8, provider queue capacity 8, retained-byte budget
536870912, ordered active batch capacity 4, and file durable adapter
explicit capacity overrides retain override provenance
invalid numeric options fail closed with first invalid option
unsupported durable adapter kind fails closed
silent borrowed-provider fallback is rejected
```

Operator summary:

```text
ready summary exposes accepted defaults
invalid configuration blocks readiness with a fix-configuration
  recommendation
failed durable state recommends retry-or-poison
claimed durable state recommends explicit claim recovery
poison durable state recommends quarantine/dead-letter action
current retained pressure blocks readiness and recommends cleanup
incompatible durable adapter state blocks readiness and recommends adapter
  inspection
```

Archive-shaped pipeline runner:

```text
deterministic RadarEventBatch input runs through the accepted
runtime/archive baseline
handler-free runs use ordered concurrent processing defaults
mergeable handler runs use handler delta/merge posture
snapshot-only handlers use explicit sequential fallback posture
unsupported handlers block with a handler-specific reason
BFF read model store publishes latest run, batches, source outputs,
diagnostics, and handler output
invalid configuration returns a blocked result without publishing a read
model
```

Durable restart/recovery:

```text
completed envelope can commit after adapter/session recreation
claimed envelope blocks restarted pipeline until explicit recovery action
failed envelope blocks recovery with retry-or-poison recommendation
incompatible durable store content fails closed
file durable adapter summary remains visible through the pipeline recovery
result
```

Rollback/fallback diagnostics:

```text
stop-accepting posture preserves durable state visibility
drain-accepted posture completes pending durable work in provider sequence
cancel-open posture cancels and releases open work explicitly
unsafe borrowed-provider fallback is rejected
snapshot-only handler fallback still publishes BFF diagnostics
```

Capacity evidence:

```text
capacity evidence captures elapsed time, measured allocation, accepted batch
count, processed count, committed count, handler mode, durable adapter kind,
terminal retained pressure, processing completeness, readiness, first
blocking reason, and configuration contour
blocked capacity evidence preserves first blocking reason
```

## Warnings To Review In Decision Trace

```text
milestone 027 validates deterministic archive-shaped production pipeline
integration, not true live network ingestion
the file durable adapter remains the local persistence baseline; Kafka,
RabbitMQ, cloud queue, and database-backed adapters are not included
the production pipeline runner composes accepted in-process backend surfaces;
it is not a production HTTP BFF host or frontend implementation
rollback/fallback posture is operator-visible and explicit; hidden borrowed
provider fallback remains rejected
capacity evidence is local representative evidence, not cross-machine
throughput certification
exactly-once production delivery is not claimed
deployment platform automation, autoscaling, alert routing, and runbooks
remain future work
```

## Pre-Decision Stop Point

Milestone 027 implementation slices and focused Release gate evidence are
complete.

Decision trace has not been written.
