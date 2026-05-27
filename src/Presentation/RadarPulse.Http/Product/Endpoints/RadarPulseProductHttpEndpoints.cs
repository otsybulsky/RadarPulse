using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Mvc;

namespace RadarPulse.Http.Product;

/// <summary>
/// Minimal API endpoint mappings for the local product pipeline HTTP surface.
/// </summary>
/// <remarks>
/// The endpoints are a thin same-origin adapter over
/// <see cref="RadarPulseProductPipelineApiContract"/>. They preserve the product
/// API envelope shape and do not add production auth, TLS, or public-hosting
/// guarantees.
/// </remarks>
public static class RadarPulseProductHttpEndpoints
{
    /// <summary>
    /// Maps product pipeline run, query, readiness, and control routes.
    /// </summary>
    public static IEndpointRouteBuilder MapRadarPulseProductPipeline(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/product/pipeline");

        group.MapPost("/runs/demo", RunDemoAsync);
        group.MapPost("/runs/archive", RunArchiveAsync);
        group.MapGet("/runs", ListRuns);
        group.MapGet("/runs/latest", GetLatestRun);
        group.MapGet("/runs/{runId}", GetRun);
        group.MapGet("/runs/{runId}/batches", ListBatches);
        group.MapGet("/runs/{runId}/batches/{providerSequence:long}", GetBatch);
        group.MapGet("/runs/{runId}/sources", ListSources);
        group.MapGet("/runs/{runId}/sources/{sourceId:int}", GetSource);
        group.MapGet("/runs/{runId}/handlers/{sourceId:int}/{fieldName}", GetHandlerOutput);
        group.MapGet("/runs/{runId}/diagnostics", GetDiagnostics);
        group.MapGet("/runs/{runId}/capacity", GetCapacityEvidence);
        group.MapGet("/host/readiness", GetHistoryReadiness);
        group.MapGet("/host/demo-readiness", GetDemoReadiness);
        group.MapPost("/controls/stop-accepting", StopAcceptingAsync);
        group.MapPost("/controls/drain-accepted", DrainAcceptedAsync);
        group.MapPost("/controls/cancel-open-release", CancelOpenAndReleaseAsync);
        group.MapPost("/controls/reject-unsafe-fallback", RejectUnsafeFallbackAsync);

        return endpoints;
    }

    /// <summary>
    /// Starts a deterministic synthetic demo run.
    /// </summary>
    public static async ValueTask<IResult> RunDemoAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await api.RunDemoAsync(request, cancellationToken)
                .ConfigureAwait(false));

    /// <summary>
    /// Starts a run from one local NEXRAD archive file.
    /// </summary>
    public static async ValueTask<IResult> RunArchiveAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await api.RunArchiveFileAsync(request, cancellationToken)
                .ConfigureAwait(false));

    /// <summary>
    /// Returns compact summaries for all visible product runs.
    /// </summary>
    public static IResult ListRuns(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.ListRuns());

    /// <summary>
    /// Returns the latest visible product run detail.
    /// </summary>
    public static IResult GetLatestRun(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.GetLatestRun());

    /// <summary>
    /// Returns one product run detail by run id.
    /// </summary>
    public static IResult GetRun(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetRun(runId));

    /// <summary>
    /// Returns all batches captured for a product run.
    /// </summary>
    public static IResult ListBatches(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.ListBatches(runId));

    /// <summary>
    /// Returns one batch by provider sequence.
    /// </summary>
    public static IResult GetBatch(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        long providerSequence) =>
        ToHttpResult(api.GetBatch(runId, providerSequence));

    /// <summary>
    /// Returns all processed source read models for a product run.
    /// </summary>
    public static IResult ListSources(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.ListSources(runId));

    /// <summary>
    /// Returns one processed source by source id.
    /// </summary>
    public static IResult GetSource(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        int sourceId) =>
        ToHttpResult(api.GetSource(runId, sourceId));

    /// <summary>
    /// Returns one handler output field for a source in a run.
    /// </summary>
    public static IResult GetHandlerOutput(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        int sourceId,
        string fieldName) =>
        ToHttpResult(api.GetHandlerOutput(runId, sourceId, fieldName));

    /// <summary>
    /// Returns diagnostic evidence for a product run.
    /// </summary>
    public static IResult GetDiagnostics(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetDiagnostics(runId));

    /// <summary>
    /// Returns local capacity and completeness evidence for a product run.
    /// </summary>
    public static IResult GetCapacityEvidence(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetCapacityEvidence(runId));

    /// <summary>
    /// Returns product history readiness for the configured host.
    /// </summary>
    public static IResult GetHistoryReadiness(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.GetHistoryReadiness());

    /// <summary>
    /// Returns composed readiness for the local demo package.
    /// </summary>
    public static IResult GetDemoReadiness(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromServices] RadarPulseProductHttpOptions options)
    {
        var historyResponse = api.GetHistoryReadiness();
        var readiness = RadarPulseProductDemoReadiness.From(
            historyResponse.Body!,
            options);
        return ToHttpResult(RadarPulseProductApiResponse<RadarPulseProductDemoReadiness>.Ok(readiness));
    }

    /// <summary>
    /// Applies the stop-accepting product control action.
    /// </summary>
    public static ValueTask<IResult> StopAcceptingAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.StopAccepting,
            cancellationToken);

    /// <summary>
    /// Applies the drain-accepted product control action.
    /// </summary>
    public static ValueTask<IResult> DrainAcceptedAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.DrainAccepted,
            cancellationToken);

    /// <summary>
    /// Applies the cancel-open-and-release product control action.
    /// </summary>
    public static ValueTask<IResult> CancelOpenAndReleaseAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.CancelOpenAndRelease,
            cancellationToken);

    /// <summary>
    /// Applies the reject-unsafe-fallback product control action.
    /// </summary>
    public static ValueTask<IResult> RejectUnsafeFallbackAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.RejectUnsafeFallback,
            cancellationToken);

    /// <summary>
    /// Converts a product API envelope into the HTTP JSON response used by all routes.
    /// </summary>
    public static IResult ToHttpResult<T>(
        RadarPulseProductApiResponse<T> response) =>
        Results.Json(
            response,
            statusCode: response.StatusCode);

    private static async ValueTask<IResult> ApplyControlAsync(
        RadarPulseProductPipelineApiContract api,
        RadarPulseProductPipelineControlRequest request,
        RadarPulseProductControlAction action,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await api.ApplyControlAsync(
                    request with { Action = action },
                    cancellationToken)
                .ConfigureAwait(false));
}
