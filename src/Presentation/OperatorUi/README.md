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

Run browser smoke tests against the Angular dev server with deterministic
product HTTP route fixtures:

```powershell
npm run smoke
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
dotnet run --project src\Presentation\RadarPulse.Http\RadarPulse.Http.csproj
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

`RadarPulse.Http` enables a scoped local CORS policy for the Angular dev
server origin:

```text
http://localhost:4200
```

That local bridge is for development and milestone validation only. It is not
production public API security hardening.

## Integrated Local Host

Milestone 031 also supports a single local host workflow where
`RadarPulse.Http` serves the built Angular bundle and the product API from the
same origin.

Build the UI first:

```powershell
npm run build
```

Then run the HTTP host:

```powershell
dotnet run --project ..\RadarPulse.Http\RadarPulse.Http.csproj --urls http://127.0.0.1:5129
```

Open:

```text
http://127.0.0.1:5129
```

When the UI is served from `RadarPulse.Http`, the default product API base URL
is the current browser origin. The dev-server default remains
`http://localhost:5000`.

The static asset root is configured by:

```text
RadarPulse:ProductHttp:OperatorUiStaticAssetPath
```

The default local development value resolves to:

```text
src/Presentation/OperatorUi/dist/OperatorUi/browser
```

The integrated hosted browser smoke gate uses a local in-memory product
history store:

```powershell
npm run smoke:hosted
```

Run `npm run build` before `npm run smoke:hosted` so the hosted path has a
fresh Angular bundle to serve.

## Product Demo/Readiness Package

Milestone 032 adds a repository-level local product demo/readiness workflow
over this same-origin host:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
```

The full operator workflow is documented in:

```text
docs/product-demo-readiness.md
```

For portfolio review, start from the repository `README.md`, then use
`docs/product-demo-readiness.md` for the operational walkthrough and visual
checkpoints.

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
authentication, authorization, TLS termination, production CORS hardening,
external broker/cloud queue/database adapter readiness, cross-machine
throughput, or exactly-once delivery.
