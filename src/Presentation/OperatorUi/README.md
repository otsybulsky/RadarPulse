# RadarPulse Operator UI

Angular product operator UI for the local RadarPulse product HTTP host.

This app is scoped to deterministic demo/archive-shaped product workflows over
the accepted `RadarPulse.Http` routes. It does not read product history files
directly, import .NET assemblies, or call lower-level application/runtime
objects.

## Local Setup

Install dependencies:

```powershell
npm install
```

Run tests:

```powershell
npm test -- --watch=false
```

Build the production bundle:

```powershell
npm run build
```

Start the Angular dev server:

```powershell
npm start
```

The dev server normally listens at:

```text
http://localhost:4200
```

## Backend

Run the local product HTTP host from the repository root:

```powershell
dotnet run --project src\Presentation.Http\RadarPulse.Http.csproj
```

The UI defaults to:

```text
http://localhost:5000
```

If the HTTP host runs on a different URL, change the `HTTP host` field in the
top bar and apply it. The override is stored in browser local storage under:

```text
radarpulse.productApiBaseUrl
```

## Operator Workflow

The UI supports:

```text
host/history readiness
deterministic demo run creation
archive-shaped run creation when a valid archive file path is supplied
persisted run list and latest run
selected run summary
batch, source, handler, diagnostics, and capacity inspection
handler output lookup by source id and field name
stop accepting, drain accepted, cancel/release, and reject unsafe fallback
  controls
explicit unreachable host, blocked history, not-found, rejected control, and
  validation/failure posture
```

## Scope Boundary

This UI is a local product operator surface. It does not claim true live radar
network ingestion, public production hosting, deployment automation,
authentication, authorization, TLS termination, CORS hardening, external
broker/cloud queue/database adapter readiness, cross-machine throughput, or
exactly-once delivery.
