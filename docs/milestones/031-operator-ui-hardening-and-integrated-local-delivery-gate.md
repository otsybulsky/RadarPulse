# Milestone 031: Operator UI Hardening And Integrated Local Delivery Gate

Status: captured; pre-decision review pending.

This gate records the implementation evidence for milestone 031. The decision
trace has not been written.

## Gate Question

```text
Is RadarPulse ready to use the Angular operator UI as the hardened local
product surface, including browser-smoke validated workflows and integrated
same-origin local delivery through RadarPulse.Http?
```

## Gate Answer Candidate

```text
yes with scoped warnings for operator UI hardening and integrated local
delivery over deterministic archive-shaped workflows
```

The answer remains scoped. This gate does not claim true live network
ingestion, external broker/cloud queue/database adapter certification, public
production hosting, auth/TLS/production CORS hardening, deployment
automation, cross-machine throughput certification, or exactly-once delivery.

## Implementation Evidence

Completed:

```text
URL-restorable selected run and active run-detail tab
selected run not-found posture from URL state
validated product HTTP base URL override
validated archive run request input
validated handler lookup input
disabled/loading/rejected/blocked control posture
Playwright browser smoke harness for dev-server UI workflows
hosted same-origin Playwright smoke harness through RadarPulse.Http
RadarPulse.Http static Angular asset delivery
operator UI fallback to index.html for local UI routes
explicit 404 posture for /product/pipeline routes that are not product API
  matches
same-origin product API base URL default when UI is served by RadarPulse.Http
dev-server default product API base URL preserved for localhost:4200
README workflow documentation for dev-server and integrated local host usage
```

Not implemented or not claimed:

```text
true live radar network ingestion
rich meteorological radar visualization
public production frontend/backend hosting
authentication or authorization
TLS termination
production CORS hardening
deployment automation
external broker/cloud queue/database adapters
cross-machine throughput certification
exactly-once end-to-end delivery
reopening accepted milestone 020-030 backend/runtime/product/UI decisions
```

## Final Verification

Angular unit gate:

```text
cd src\Presentation\OperatorUi
npm test -- --watch=false

result:
  20 passed, 0 failed
```

Angular production build:

```text
cd src\Presentation\OperatorUi
npm run build

result:
  succeeded, 0 warnings
```

Browser smoke gate over Angular dev-server workflow:

```text
cd src\Presentation\OperatorUi
npm run smoke

result:
  4 passed, 0 failed
```

Browser smoke gate over integrated same-origin RadarPulse.Http workflow:

```text
cd src\Presentation\OperatorUi
npm run smoke:hosted

result:
  1 passed, 0 failed
```

Focused .NET product HTTP/API/static-delivery Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

result:
  18 passed, 0 failed, 0 skipped
```

.NET Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Smoke Coverage

Dev-server smoke covers:

```text
readiness rendering
persisted run list rendering
deep-linked selected run and diagnostics tab
demo run creation
URL state preservation for run id and active tab
handler output lookup
rejected control posture
unreachable host posture
disabled unsafe controls while host is unreachable
```

Hosted same-origin smoke covers:

```text
RadarPulse.Http serves the built Angular shell
RadarPulse.Http product API returns JSON from the same origin
the UI defaults the product API base URL to the hosted origin
demo run creation works through the hosted same-origin API
selected run and capacity tab survive reload through URL state
```

## Scope Warnings For Decision Review

Carry these warnings into the decision trace unless explicitly changed:

```text
the integrated UI delivery path is local same-origin hosting through
  RadarPulse.Http, not public production deployment
the hosted smoke uses deterministic demo/archive-shaped product workflows,
  not true live radar network ingestion
the Angular dev-server CORS bridge remains a local development bridge and is
  not production public API security hardening
same-origin local delivery does not add authentication, authorization, TLS
  termination, production CORS hardening, deployment automation, autoscaling,
  alert routing, or operator runbooks
the static asset root expects a built Angular bundle; RadarPulse.Http does
  not perform frontend build orchestration at runtime
external broker/cloud queue/database adapters remain outside the project plan
cross-machine throughput certification and exactly-once delivery are not
  claimed
accepted milestone 020-030 backend decisions are not reopened
```

## Pre-Decision Stop Point

Implementation slices are complete and gate evidence is captured.

Stop here before writing:

```text
031-operator-ui-hardening-and-integrated-local-delivery-decision-trace.md
031-operator-ui-hardening-and-integrated-local-delivery-closeout.md
```
