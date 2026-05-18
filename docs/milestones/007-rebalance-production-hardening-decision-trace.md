# Milestone 007: Decision Trace

Status: implementation substantially complete; final performance closeout
pending.

Closeout: `007-rebalance-production-hardening-closeout.md` (pending).

## 1. What Was Implemented

Milestone 007 hardened the synchronous milestone 006 rebalance control plane
before adding retained async worker transport:

- Hardening option contracts for telemetry retention, quarantine lifecycle, and
  validation profiles.
- Bounded rebalance telemetry contracts and a telemetry recorder that retains
  counters plus capped recent detail.
- Quarantine lifecycle state, transitions, evaluator, planner integration, and
  session result surfaces.
- Validation profiles: `off`, `essential`, `diagnostic`, and `benchmark`.
- Allocation attribution for processing-only synthetic runs and archive replay
  runs, including separate processing-callback allocation for archive contours.
- Allocation-reduction passes for no-move, skipped-only, and capped-detail
  paths.
- Synthetic quarantine lifecycle workloads for TTL retry, sustained cooling
  clear, material pressure-change retry, retry re-entry, and successful relief
  clear.
- Synthetic retention stress workloads for long no-hot, cooldown rejection,
  unsafe-target rejection, mixed skipped reasons, and counters-only retention.
- Archive rebalance benchmark retention controls, retained-detail counters,
  skipped-reason counters, validation profile output, and pressure-skew stress
  overlay.
- CLI hardening controls for validation profile, telemetry retention, pressure
  skew, and quarantine lifecycle options.
- Policy default audit:
  `007-rebalance-production-hardening-policy-default-audit.md`.

The synchronous `PartitionedBarrier` processing boundary remains the reference
correctness path: one batch is processed against one topology snapshot, then
rebalance may publish a new topology before the next batch.

## 2. Decision Matrix

### Harden Before Async Worker Transport

Decision: finish production hardening of the synchronous rebalance control plane
before adding retained async worker queues or worker-local state transport.

Why chosen: milestone 006 proved ownership movement, migration validation, and
state handoff at a deterministic batch boundary. Milestone 007 needed to make
that control plane bounded, observable, and tunable before introducing a
background scheduler, retained payload protocol, or multi-worker failure modes.

Alternatives: implement async worker transport immediately, build retained
queues first, or tune scheduler behavior before telemetry and validation were
bounded.

Rejected because: async transport would hide control-plane defects behind
scheduling noise, retained queues require new payload lifetime rules, and
tuning without bounded telemetry could create unbounded diagnostic retention in
long-running sessions.

Trade-offs/debt: milestone 007 still does not prove physical worker-local state
transfer or retained async processing. That remains future work after the
synchronous reference path is hardened.

Review explanation: "The controller should be safe and measurable before it is
made asynchronous."

### Quarantine Is Temporary And Evidence-Based

Decision: make hot-partition quarantine a lifecycle state that can clear,
become retry-eligible, or re-enter quarantine based on logical evidence.

Why chosen: milestone 006 could quarantine ineffective hot partitions, but a
permanent classification would become an eternal ban. Milestone 007 added TTL,
sustained cooling, and material pressure-change evidence so stale quarantine
does not block safe future relief.

Alternatives: keep quarantine permanent, clear quarantine immediately on any
cool sample, retry every evaluation, or remove quarantine entirely.

Rejected because: permanent quarantine hides recoverable pressure states, a
single cool sample is too noisy, retrying every evaluation can create churn,
and removing quarantine reopens repeated ineffective-move loops.

Trade-offs/debt: the accepted defaults are intentionally conservative:
`64` TTL evaluations, `3` sustained cooling samples, and `0.25` material
pressure change. Runtime-specific values can be tuned through explicit CLI
flags and future configuration.

Review explanation: "Quarantine blocks repeated bad moves, but it has an exit
path when the evidence changes."

### Bounded Telemetry Retention

Decision: keep aggregate counters forever within a run, but retain detail only
through bounded windows controlled by telemetry retention options.

Why chosen: long archive runs need to explain accepted moves, skipped reasons,
quarantine lifecycle transitions, and validation failures without retaining
unbounded decision history. Counters preserve totals; capped recent detail
preserves examples for diagnosis.

Alternatives: retain every decision, drop all detail, or only print summaries
from the CLI.

Rejected because: retaining every decision is unsafe for long sessions, dropping
all detail makes troubleshooting opaque, and CLI-only summaries do not help
library callers or tests.

Trade-offs/debt: recent detail is a sample, not a full history. Operators who
need exact full traces should request an explicit diagnostic trace artifact in
a future milestone.

Review explanation: "Counts tell us how often; bounded recent detail tells us
what it looked like."

### Validation Profiles

Decision: expose validation profiles as `off`, `essential`, `diagnostic`, and
`benchmark`.

Why chosen: milestone 006 validation was intentionally strong, but milestone
007 needed an explicit way to separate steady-state checks from diagnostic and
benchmark costs. Profiles let the same session shape run with different
validation expense while reporting which profile was used.

Alternatives: keep one validation mode, make validation a boolean, or disable
validation in benchmarks by default.

Rejected because: one mode hides cost, a boolean loses useful distinction
between essential publication checks and full diagnostics, and disabling
validation by default weakens closeout evidence.

Trade-offs/debt: `diagnostic` remains the accepted default for tests and
closeout benchmark rows. A future production runtime may default to
`essential` after operational requirements are clearer.

Review explanation: "Validation is not free, so every benchmark row should say
which validation bill it paid."

### Allocation Attribution Separates Replay From Processing

Decision: report processing-callback allocation separately from replay and
batch-construction allocation for archive rebalance benchmarks.

Why chosen: archive replay, decompression, identity normalization, and batch
construction are intentionally outside the processing rebalance callback. A
single allocation number would blur archive mechanics with control-plane
allocation and make milestone 005/006 comparisons misleading.

Alternatives: report only end-to-end allocation, report only processing
allocation, or ignore allocation in archive contours.

Rejected because: end-to-end-only hides callback behavior, callback-only hides
the measured archive contour, and ignoring allocation would miss one of the
milestone 007 hardening goals.

Trade-offs/debt: callback allocation attribution depends on the benchmark
contour and should not be treated as a complete memory profile. It is a
comparison metric, not a profiler replacement.

Review explanation: "Archive benchmarks have two useful allocation numbers:
what the replay path spent and what the rebalance callback spent."

### Allocation Reduction Targets Control-Plane Churn

Decision: reduce avoidable allocation in no-move, skipped-only, and bounded
detail paths while preserving diagnostic output.

Why chosen: real-data cache runs spend many evaluations on no-action or
policy-gated outcomes. Those paths must remain allocation-light after warmup,
especially when telemetry retention is counters-only or capped.

Alternatives: remove detail entirely, optimize only accepted moves, or defer
allocation work to the async milestone.

Rejected because: removing detail weakens diagnostics, accepted moves are not
the only repeated path, and async work should inherit a low-churn synchronous
control plane rather than fix it later.

Trade-offs/debt: this does not guarantee zero allocation for every diagnostic
mode. Diagnostic retention intentionally pays for bounded detail.

Review explanation: "The common no-move path should not allocate just to say
nothing moved."

### Synthetic Workloads Are Behavioral Contours

Decision: expand synthetic rebalance workloads for lifecycle and retention
behavior, but keep them as deterministic behavioral contours rather than
throughput-scale workloads.

Why chosen: tiny workloads make specific decisions, lifecycle transitions, and
retention counters easy to assert. They are ideal for correctness and same-run
static/sampling/rebalance ratios.

Alternatives: make all synthetic workloads large throughput benchmarks, or
test lifecycle behavior only through archive data.

Rejected because: large workloads obscure exact lifecycle outcomes, while
archive-only tests make edge cases hard to reproduce deterministically.

Trade-offs/debt: synthetic throughput should not be compared directly to the
milestone 005 large processing-only baseline. The closeout must preserve this
caveat and use same-run ratios for synthetic overhead interpretation.

Review explanation: "Synthetic rebalance tests are microscopes, not production
load generators."

### Archive Pressure Skew Is Benchmark-Only

Decision: add pressure skew as an explicit benchmark-only overlay that modifies
effective pressure samples for planning while leaving archive payloads and
observed telemetry unchanged.

Why chosen: the full local archive cache has limited natural rebalance activity.
Pressure skew lets real archive replay exercise active rebalance contours
without rewriting cached files or confusing validation of observed stream data.

Alternatives: mutate archive events, synthesize fake archive files, lower
production pressure thresholds globally, or treat skewed results as baseline
real-data performance.

Rejected because: mutating data undermines replay validation, fake files do not
exercise real replay shape, lowering defaults changes controller policy without
evidence, and skewed results answer a different question than baseline real
data.

Trade-offs/debt: skewed archive runs must be labeled clearly as "real archive
with synthetic pressure overlay." They are stress contours, not baseline
throughput evidence.

Review explanation: "Skew makes the planner work harder without pretending the
radar data changed."

### Defaults Remain Conservative After Audit

Decision: accept the current hardening, policy, pressure, skew, and benchmark
CLI defaults without code changes.

Why chosen: the policy-default audit found the defaults conservative, bounded,
and observable. They preserve milestone 006 behavior unless a hardening feature
explicitly adds bounded diagnostics or lifecycle exits.

Alternatives: lower quarantine TTL, make validation `essential` by default,
enable pressure skew by default, increase archive benchmark parallelism by
default, or loosen policy budgets.

Rejected because: none had stronger evidence than the current defaults. More
aggressive defaults could hide churn, make baseline comparisons less stable, or
blur benchmark-only stress with production-shaped behavior.

Trade-offs/debt: future production deployments may choose different defaults
once operational requirements and live telemetry exist. Such changes should be
explicit, measured, and separated from milestone 007 closeout baselines.

Review explanation: "The new knobs exist, but the default controller stays
cautious."

### CLI Hardening Surface Is Additive

Decision: expose validation, retention, pressure skew, and quarantine lifecycle
controls through benchmark CLI options while preserving existing command
defaults.

Why chosen: closeout and future tuning need reproducible command lines for
specific hardening contours. Existing smoke and benchmark commands should not
change behavior unless a new option is passed.

Alternatives: add separate commands for every hardening contour, change defaults
silently, or leave hardening controls only in library APIs.

Rejected because: command explosion makes comparison harder, silent default
changes break baselines, and library-only controls make manual performance
investigation too slow.

Trade-offs/debt: CLI usage strings are now long. A future command help system
may need structured help output.

Review explanation: "Every hardening contour should be reproducible from a
command line."

### Final Performance Gate Must Separate Contours

Decision: the milestone 007 closeout must compare default, counters-only,
validation-profile, and pressure-skew contours separately against accepted
milestone 005 and 006 baselines.

Why chosen: no single benchmark row can answer every performance question.
Default hardening proves normal behavior, counters-only retention isolates
retention cost, validation profiles expose validation cost, and pressure skew
exercises active rebalance stress.

Alternatives: report only full-cache default numbers, report only synthetic
ratios, or collapse skewed and non-skewed archive runs into one table.

Rejected because: default-only misses hardening costs, synthetic-only misses
real archive replay shape, and mixing skewed/non-skewed rows would make the
baseline ambiguous.

Trade-offs/debt: the final comparison will be a matrix rather than one headline
number. The interpretation must call out replay-dominated end-to-end metrics
versus processing-callback metrics.

Review explanation: "The closeout needs several labeled contours, not one
averaged score."

## 3. Remaining Risks

- Final milestone 007 performance comparison is still pending.
- Decision trace is complete, but closeout still needs captured Release tables
  and interpretation.
- Async worker transport, retained payload queues, and physical worker-local
  state transfer remain future milestones.
- Pressure skew is useful for stress, but baseline real-data captures must keep
  `--skew-profile none`.
