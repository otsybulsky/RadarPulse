# Milestone 018: Reporting, Contract, And Harness Gap Closure

Status: captured for slice 3.

This document records milestone 018 slice 3. It maps the existing result
contracts to the runtime readiness gate fields, decides which reporting gaps
must be closed before gate capture, and defines the temporary gate harness
schema.

Runtime behavior changes in this slice:

```text
none
```

Production code changes in this slice:

```text
none
```

## Slice 3 Decision

The existing lower-level contracts are sufficient to proceed to runtime gate
capture without adding a production runtime API or CLI command first.

The missing "single runtime readiness result" surface will be handled by a
temporary local gate harness that joins existing producer, consumer, queue,
overlap, retention, pressure, worker, and processing-completeness fields into
JSONL rows and Markdown summaries.

This is intentionally not a runtime default migration:

```text
queued-owned remains an explicit candidate in the gate harness
runtime default posture remains undecided
runtime prewarm posture remains undecided until slice 4
no production CLI runtime command is introduced in slice 3
```

## Contract Mapping

### Provider And Intake

Source contracts:

```text
RadarProcessingArchiveQueuedOverlapResult
RadarProcessingArchiveQueuedOverlapProducerResult
RadarProcessingArchiveQueuedProviderResult
RadarProcessingQueuedBatchEnqueueResult
RadarProcessingProviderQueueTelemetrySummary
```

Gate fields:

```text
producer status
producer message
provider publish attempt count
accepted publish count
rejected publish count
last enqueue status
enqueue attempts
enqueued batches
enqueue full count
enqueue timed out count
enqueue canceled count
enqueue closed count
enqueue faulted count
total enqueue wait time
queue depth high-water
queued payload bytes high-water
```

Status:

```text
available from existing contracts
```

### Consumer And Processing Completion

Source contracts:

```text
RadarProcessingArchiveQueuedOverlapConsumerResult
RadarProcessingQueuedSessionResult
RadarProcessingQueuedBatchProcessingResult
RadarProcessingProviderQueueTelemetrySummary
RadarProcessingWorkerTelemetrySummary
```

Gate fields:

```text
consumer status
consumer message
queued session status
processing result count
completed batch count
failed batch count
canceled batch count
skipped-after-fault count
worker failed batches
worker failed work items
processing validation failed batches
processing completeness
final topology version
total drain time
```

Status:

```text
mostly available from existing contracts
```

Harness responsibility:

```text
compute processing completeness for runtime rows using:
  session completed
  failed batch count == 0
  canceled batch count == 0
  worker failed batches/items == 0/0 where worker telemetry is available
  validation/failure statuses absent from processing result rows
```

### Retained Payload And Release

Source contracts:

```text
RadarProcessingArchiveQueuedProviderResult.RetentionTelemetry
RadarProcessingRetainedPayloadTelemetrySummary
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingRetainedResourcePressureSummary
```

Gate fields:

```text
retention strategy
retention attempts
retained batch count
retained event count
retained payload bytes
retention allocated bytes
retained pool rents/returns/misses
event pool rents/returns/misses
payload pool rents/returns/misses
release attempts
released batch count
release failed count
release not required count
total retention time
total release time
```

Status:

```text
available from existing contracts
```

### Pressure And Cleanup

Source contracts:

```text
RadarProcessingRetainedResourcePressureSummary
RadarProcessingArchiveOverlapTelemetrySummary
RadarProcessingProviderQueueTelemetrySummary
```

Gate fields:

```text
current pending retained batch count
current pending retained payload bytes
current active retained batch count
current active retained payload bytes
current combined retained batch count
current combined retained payload bytes
pending retained high-water
active retained high-water
combined retained high-water
retained cleanup succeeded
retained pressure within budget
```

Status:

```text
available from existing contracts
```

Harness responsibility:

```text
classify cleanup as passed only when current pending, active, and combined
counts/bytes are zero at terminal state
```

### Prewarm

Source contracts:

```text
RadarProcessingRetainedPayloadFactory.Prewarm()
RadarProcessingRetainedPayloadPrewarmResult
direct benchmark retained payload prewarm result fields
```

Gate fields:

```text
prewarm enabled
prewarm source
prewarm event count
prewarm payload bytes
prewarm retained batch count
prewarm elapsed ms
prewarm allocated bytes
prewarm retained bytes
```

Status:

```text
available as a primitive and as direct benchmark attribution
runtime lifecycle policy undecided until slice 4
```

Harness responsibility:

```text
if slice 4 selects a runtime prewarm candidate, record prewarm as a separate
lifecycle record before intake starts and keep it outside steady measured
allocation interpretation
```

### Provenance

Source contracts:

```text
direct benchmark option provenance in CLI/result paths
runtime gate harness configuration
RadarProcessingArchiveProviderMode
RadarProcessingQueuedProviderOverlapMode
RadarProcessingRetainedPayloadStrategy
RadarProcessingExecutionMode
RadarProcessingProviderQueueOptions
```

Gate fields:

```text
surface
scenario id
scenario group
provider mode
provider mode source
default candidate contour
fallback/reference contour
retention strategy
provider overlap mode
execution mode
worker count
worker queue capacity
provider queue capacity
retained-byte budget
```

Status:

```text
available by explicit gate harness configuration
```

Harness responsibility:

```text
do not infer runtime default posture from direct benchmark omitted defaults
label queued-owned runtime rows as explicit-candidate until decision trace
label borrowed/reference rows as explicit-reference
```

## Temporary Harness Schema

Suggested ignored runner location:

```text
data\temp\m018-runtime-gate-runner
```

Suggested output directory:

```text
data\temp\m018-runtime-gate-runner\output
```

Required output files:

```text
m018-runtime-<timestamp>.jsonl
m018-runtime-<timestamp>.md
```

Required JSONL record types:

```text
header
scenario
measurement
terminal-cleanup
group-summary
run-summary
```

### Header Record

Required fields:

```text
type
runId
timestampUtc
milestone
slice
buildConfiguration
repositoryRoot
gateMatrixVersion
runtimeDefaultPosture
prewarmPosture
retainedByteBudget
notes
```

### Scenario Record

Required fields:

```text
type
runId
scenarioId
scenarioGroup
description
inputKind
cachePath
date
radarId
maxFiles
expectedPublishedFiles
expectedSkippedFiles
providerMode
providerModeSource
fallbackReference
defaultCandidate
queueCapacity
queueFullMode
enqueueTimeoutMs
shutdownMode
retainedByteBudget
retentionStrategy
executionMode
workerCount
workerQueueCapacity
```

### Measurement Record

Required fields:

```text
type
runId
scenarioId
rowId
rowKind
status
message
elapsedMs
producerStatus
consumerStatus
sessionStatus
providerPublishAttempts
providerAcceptedPublishes
providerRejectedPublishes
lastEnqueueStatus
enqueueAttempts
enqueuedBatches
enqueueFullCount
enqueueTimedOutCount
enqueueCanceledCount
enqueueClosedCount
enqueueFaultedCount
queueDepthHighWatermark
queuedPayloadBytesHighWatermark
totalEnqueueWaitMs
totalDequeueWaitMs
totalDrainMs
retentionStrategy
retainedBatchCount
retentionAllocatedBytes
retentionElapsedMs
releaseAttemptCount
releaseFailedCount
currentPendingRetainedBytes
currentActiveRetainedBytes
currentCombinedRetainedBytes
combinedRetainedBytesHighWatermark
retainedPressureWithinBudget
processingCompleteness
processingResultCount
processingCompletedCount
processingFailedCount
processingCanceledCount
processingSkippedAfterFaultCount
workerFailedBatchCount
workerFailedWorkItemCount
finalTopologyVersion
prewarmEnabled
prewarmSource
prewarmElapsedMs
prewarmAllocatedBytes
prewarmRetainedBytes
classification
classificationReasons
```

### Terminal Cleanup Record

Required fields:

```text
type
runId
scenarioId
terminalStatus
currentPendingRetainedBatchCount
currentPendingRetainedPayloadBytes
currentActiveRetainedBatchCount
currentActiveRetainedPayloadBytes
currentCombinedRetainedBatchCount
currentCombinedRetainedPayloadBytes
releaseFailedCount
providerOverlapReleaseFailedCount
cleanupPassed
```

### Group Summary Record

Required fields:

```text
type
runId
groupId
scenarioCount
passCount
warningCount
optimizeCount
failCount
coverageOnlyCount
blockedCount
maxCombinedRetainedBytesHighWatermark
maxQueueDepthHighWatermark
totalReleaseFailedCount
totalWorkerFailedBatchCount
totalWorkerFailedWorkItemCount
processingCompletenessFailureCount
summaryStatus
summaryReasons
```

## Markdown Summary Requirements

The Markdown summary should include:

```text
run header
candidate runtime posture
prewarm posture
included surfaces
excluded surfaces
scenario table
measurement table
terminal cleanup table
group summary table
non-negotiable failure list
coverage gaps
interpretation notes
raw JSONL path
```

## Product Reporting Decision

Slice 3 does not add production result contracts or CLI output.

Reason:

```text
existing lower-level contracts expose enough data to generate reviewable
runtime readiness evidence through a temporary local harness

production runtime defaults are not accepted yet

adding a durable operator surface before the runtime posture decision would
create an API shape before the accepted runtime policy is known
```

Carry-forward:

```text
if slice 6/7 gates show that temporary harness output is not enough to explain
runtime readiness, add focused product result fields before decision trace

if milestone 018 accepts runtime defaults, the closeout or next milestone
should decide the durable operator/CLI/API reporting surface
```

## Focused Verification Posture

Slice 3 verification is documentation and contract mapping only.

No tests are required for this slice because no product behavior changed.

Before gate capture, run focused tests from the plan:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingWorkerLifecycleContractTests|FullyQualifiedName~RadarProcessingWorkerTelemetryContractTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

## Slice 3 Completion

Slice 3 closes these gaps:

```text
maps existing lower-level contracts to every required runtime gate field
defines temporary JSONL/Markdown harness schema
decides no production reporting/API change is needed before gate capture
keeps runtime default posture undecided
keeps runtime prewarm posture deferred to slice 4
```

Gaps carried forward:

```text
actual temporary runner implementation/capture remains for slice 6/7
runtime prewarm policy remains for slice 4
CancelQueued behavior remains for slice 5
true live ingestion remains a coverage gap unless new scope is added
durable operator/runtime reporting surface remains future work unless gates
show product reporting is required before decision trace
```

Proceed to slice 4:

```text
runtime prewarm lifecycle decision and guardrails
```
