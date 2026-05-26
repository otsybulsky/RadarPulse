using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Mvc;

namespace RadarPulse.Http.Product;

public static class RadarPulseProductHttpEndpoints
{
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
        group.MapPost("/controls/stop-accepting", StopAcceptingAsync);
        group.MapPost("/controls/drain-accepted", DrainAcceptedAsync);
        group.MapPost("/controls/cancel-open-release", CancelOpenAndReleaseAsync);
        group.MapPost("/controls/reject-unsafe-fallback", RejectUnsafeFallbackAsync);

        return endpoints;
    }

    public static async ValueTask<IResult> RunDemoAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await api.RunDemoAsync(request, cancellationToken)
                .ConfigureAwait(false));

    public static async ValueTask<IResult> RunArchiveAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken) =>
        ToHttpResult(
            await api.RunArchiveFileAsync(request, cancellationToken)
                .ConfigureAwait(false));

    public static IResult ListRuns(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.ListRuns());

    public static IResult GetLatestRun(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.GetLatestRun());

    public static IResult GetRun(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetRun(runId));

    public static IResult ListBatches(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.ListBatches(runId));

    public static IResult GetBatch(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        long providerSequence) =>
        ToHttpResult(api.GetBatch(runId, providerSequence));

    public static IResult ListSources(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.ListSources(runId));

    public static IResult GetSource(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        int sourceId) =>
        ToHttpResult(api.GetSource(runId, sourceId));

    public static IResult GetHandlerOutput(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId,
        int sourceId,
        string fieldName) =>
        ToHttpResult(api.GetHandlerOutput(runId, sourceId, fieldName));

    public static IResult GetDiagnostics(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetDiagnostics(runId));

    public static IResult GetCapacityEvidence(
        [FromServices] RadarPulseProductPipelineApiContract api,
        string runId) =>
        ToHttpResult(api.GetCapacityEvidence(runId));

    public static IResult GetHistoryReadiness(
        [FromServices] RadarPulseProductPipelineApiContract api) =>
        ToHttpResult(api.GetHistoryReadiness());

    public static ValueTask<IResult> StopAcceptingAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.StopAccepting,
            cancellationToken);

    public static ValueTask<IResult> DrainAcceptedAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.DrainAccepted,
            cancellationToken);

    public static ValueTask<IResult> CancelOpenAndReleaseAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.CancelOpenAndRelease,
            cancellationToken);

    public static ValueTask<IResult> RejectUnsafeFallbackAsync(
        [FromServices] RadarPulseProductPipelineApiContract api,
        [FromBody] RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken) =>
        ApplyControlAsync(
            api,
            request,
            RadarPulseProductControlAction.RejectUnsafeFallback,
            cancellationToken);

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
