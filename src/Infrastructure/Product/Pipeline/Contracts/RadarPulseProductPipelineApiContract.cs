using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

/// <summary>
/// Product-facing in-process API contract over the production pipeline service.
/// </summary>
/// <remarks>
/// This class is the stable boundary used by HTTP endpoints, CLI workflows, and
/// tests. It translates service query results and client-input exceptions into
/// product API response envelopes while preserving the accepted backend behavior.
/// </remarks>
public sealed class RadarPulseProductPipelineApiContract
{
    private readonly RadarPulseProductPipelineService service;

    /// <summary>
    /// Creates an API contract over the supplied service or a default in-memory service.
    /// </summary>
    public RadarPulseProductPipelineApiContract(
        RadarPulseProductPipelineService? service = null)
    {
        this.service = service ?? new RadarPulseProductPipelineService();
    }

    /// <summary>
    /// Runs the deterministic synthetic product pipeline and returns the created run detail.
    /// </summary>
    /// <returns>
    /// A 201 response on success or a 400 response when request validation fails.
    /// </returns>
    public async ValueTask<RadarPulseProductApiResponse<RadarPulseProductRunDetail>> RunDemoAsync(
        RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detail = await service.RunSyntheticAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return RadarPulseProductApiResponse<RadarPulseProductRunDetail>.Created(detail);
        }
        catch (Exception exception) when (IsClientRequestException(exception))
        {
            return RadarPulseProductApiResponse<RadarPulseProductRunDetail>.BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Runs the product pipeline over a local NEXRAD archive file.
    /// </summary>
    /// <returns>
    /// A 201 response on success or a 400 response when the local archive request
    /// cannot be accepted.
    /// </returns>
    public async ValueTask<RadarPulseProductApiResponse<RadarPulseProductRunDetail>> RunArchiveFileAsync(
        RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detail = await service.RunArchiveFileAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return RadarPulseProductApiResponse<RadarPulseProductRunDetail>.Created(detail);
        }
        catch (Exception exception) when (IsClientRequestException(exception))
        {
            return RadarPulseProductApiResponse<RadarPulseProductRunDetail>.BadRequest(exception.Message);
        }
    }

    /// <summary>
    /// Returns compact summaries for all visible product runs.
    /// </summary>
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>> ListRuns() =>
        RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>>.Ok(service.ListRuns());

    /// <summary>
    /// Returns the latest visible product run detail.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetLatestRun() =>
        FromQuery(service.TryGetLatestRun());

    /// <summary>
    /// Returns one product run detail by run id.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetRun(
        string runId) =>
        FromQuery(service.TryGetRun(runId));

    /// <summary>
    /// Lists provider batches captured for a product run.
    /// </summary>
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductBatch>> ListBatches(
        string runId) =>
        FromQuery(service.ListBatches(runId));

    /// <summary>
    /// Returns one batch by provider sequence within a product run.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductBatch> GetBatch(
        string runId,
        long providerSequence) =>
        FromQuery(service.TryGetBatch(runId, providerSequence));

    /// <summary>
    /// Lists processed source read models for a product run.
    /// </summary>
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductSource>> ListSources(
        string runId) =>
        FromQuery(service.ListSources(runId));

    /// <summary>
    /// Returns one processed source read model by source id.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductSource> GetSource(
        string runId,
        int sourceId) =>
        FromQuery(service.TryGetSource(runId, sourceId));

    /// <summary>
    /// Returns one handler output value for a source in a product run.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductHandlerOutput> GetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName) =>
        FromQuery(service.TryGetHandlerOutput(runId, sourceId, fieldName));

    /// <summary>
    /// Returns diagnostic evidence for a product run.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductDiagnostics> GetDiagnostics(
        string runId) =>
        FromQuery(service.TryGetDiagnostics(runId));

    /// <summary>
    /// Returns local capacity and completeness evidence for a product run.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductCapacityEvidence> GetCapacityEvidence(
        string runId) =>
        FromQuery(service.TryGetCapacityEvidence(runId));

    /// <summary>
    /// Returns readiness and load posture for product run history.
    /// </summary>
    public RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness> GetHistoryReadiness() =>
        RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness>.Ok(
            service.HistoryReadiness);

    /// <summary>
    /// Applies a product control action to recoverable pipeline state.
    /// </summary>
    /// <returns>
    /// A 200 response when the action produced a control summary, 404 when the
    /// target state cannot be found, or 400 when the request is invalid.
    /// </returns>
    public async ValueTask<RadarPulseProductApiResponse<RadarPulseProductControlSummary>> ApplyControlAsync(
        RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return FromQuery(
                await service.ApplyControlAsync(request, cancellationToken)
                    .ConfigureAwait(false));
        }
        catch (Exception exception) when (IsClientRequestException(exception))
        {
            return RadarPulseProductApiResponse<RadarPulseProductControlSummary>.BadRequest(exception.Message);
        }
    }

    private static RadarPulseProductApiResponse<T> FromQuery<T>(
        RadarPulseProductQueryResult<T> result) =>
        result.Found
            ? RadarPulseProductApiResponse<T>.Ok(result.Value!)
            : RadarPulseProductApiResponse<T>.NotFound(result.Message);

    private static bool IsClientRequestException(
        Exception exception) =>
        exception is ArgumentException or InvalidOperationException or FormatException or IOException or InvalidDataException;
}
