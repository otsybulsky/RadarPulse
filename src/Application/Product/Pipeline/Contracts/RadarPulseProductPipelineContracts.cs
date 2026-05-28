namespace RadarPulse.Application.Product;

/// <summary>
/// Application service port for product pipeline run, query, history, and control use cases.
/// </summary>
/// <remarks>
/// Infrastructure implements this port with the accepted production-shaped
/// processing runtime and history adapters. Presentation code should depend on
/// this Application contract instead of concrete Infrastructure services.
/// </remarks>
public interface IRadarPulseProductPipelineService
{
    /// <summary>
    /// Number of run details currently visible through the configured history store.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Current readiness and load posture for the configured history store.
    /// </summary>
    RadarPulseProductRunHistoryReadiness HistoryReadiness { get; }

    /// <summary>
    /// Runs the accepted production-shaped pipeline over deterministic synthetic input.
    /// </summary>
    ValueTask<RadarPulseProductRunDetail> RunSyntheticAsync(
        RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the accepted production-shaped pipeline over one local NEXRAD archive file.
    /// </summary>
    ValueTask<RadarPulseProductRunDetail> RunArchiveFileAsync(
        RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists compact product run summaries from the configured history store.
    /// </summary>
    IReadOnlyList<RadarPulseProductRunSummary> ListRuns();

    /// <summary>
    /// Attempts to load one product run detail by run id.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetRun(string runId);

    /// <summary>
    /// Attempts to load the latest product run detail from history.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductRunDetail> TryGetLatestRun();

    /// <summary>
    /// Lists all provider batches captured for a product run.
    /// </summary>
    RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductBatch>> ListBatches(string runId);

    /// <summary>
    /// Looks up one batch by provider sequence within a product run.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductBatch> TryGetBatch(
        string runId,
        long providerSequence);

    /// <summary>
    /// Lists all processed source read models for a product run.
    /// </summary>
    RadarPulseProductQueryResult<IReadOnlyList<RadarPulseProductSource>> ListSources(string runId);

    /// <summary>
    /// Looks up one processed source by dense source id within a product run.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductSource> TryGetSource(
        string runId,
        int sourceId);

    /// <summary>
    /// Looks up one exported handler output field for a source in a product run.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductHandlerOutput> TryGetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName);

    /// <summary>
    /// Returns diagnostic evidence for a product run when diagnostics were captured.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductDiagnostics> TryGetDiagnostics(string runId);

    /// <summary>
    /// Returns capacity and completeness evidence for a product run.
    /// </summary>
    RadarPulseProductQueryResult<RadarPulseProductCapacityEvidence> TryGetCapacityEvidence(string runId);

    /// <summary>
    /// Applies a product control action against recoverable durable pipeline state.
    /// </summary>
    ValueTask<RadarPulseProductQueryResult<RadarPulseProductControlSummary>> ApplyControlAsync(
        RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Application-facing product API contract used by Presentation adapters.
/// </summary>
/// <remarks>
/// This interface keeps HTTP and CLI adapters pointed at Application vocabulary.
/// Infrastructure remains responsible for implementing the underlying service
/// port and runtime adapters.
/// </remarks>
public interface IRadarPulseProductPipelineApi
{
    /// <summary>
    /// Runs the deterministic synthetic product pipeline and returns the created run detail.
    /// </summary>
    ValueTask<RadarPulseProductApiResponse<RadarPulseProductRunDetail>> RunDemoAsync(
        RadarPulseProductPipelineSyntheticRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the product pipeline over a local NEXRAD archive file.
    /// </summary>
    ValueTask<RadarPulseProductApiResponse<RadarPulseProductRunDetail>> RunArchiveFileAsync(
        RadarPulseProductPipelineArchiveFileRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns compact summaries for all visible product runs.
    /// </summary>
    RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>> ListRuns();

    /// <summary>
    /// Returns the latest visible product run detail.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetLatestRun();

    /// <summary>
    /// Returns one product run detail by run id.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetRun(string runId);

    /// <summary>
    /// Lists provider batches captured for a product run.
    /// </summary>
    RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductBatch>> ListBatches(string runId);

    /// <summary>
    /// Returns one batch by provider sequence within a product run.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductBatch> GetBatch(
        string runId,
        long providerSequence);

    /// <summary>
    /// Lists processed source read models for a product run.
    /// </summary>
    RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductSource>> ListSources(string runId);

    /// <summary>
    /// Returns one processed source read model by source id.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductSource> GetSource(
        string runId,
        int sourceId);

    /// <summary>
    /// Returns one handler output value for a source in a product run.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductHandlerOutput> GetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName);

    /// <summary>
    /// Returns diagnostic evidence for a product run.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductDiagnostics> GetDiagnostics(string runId);

    /// <summary>
    /// Returns local capacity and completeness evidence for a product run.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductCapacityEvidence> GetCapacityEvidence(string runId);

    /// <summary>
    /// Returns readiness and load posture for product run history.
    /// </summary>
    RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness> GetHistoryReadiness();

    /// <summary>
    /// Applies a product control action to recoverable pipeline state.
    /// </summary>
    ValueTask<RadarPulseProductApiResponse<RadarPulseProductControlSummary>> ApplyControlAsync(
        RadarPulseProductPipelineControlRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Product-facing in-process API contract over the product pipeline service port.
/// </summary>
/// <remarks>
/// This Application-owned contract is the stable boundary used by HTTP
/// endpoints, CLI workflows, and tests. It translates service query results
/// and client-input exceptions into product API response envelopes while
/// preserving the accepted backend behavior.
/// </remarks>
public sealed class RadarPulseProductPipelineApiContract : IRadarPulseProductPipelineApi
{
    private readonly IRadarPulseProductPipelineService service;

    /// <summary>
    /// Creates an API contract over the supplied Application product service port.
    /// </summary>
    public RadarPulseProductPipelineApiContract(
        IRadarPulseProductPipelineService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>> ListRuns() =>
        RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>>.Ok(service.ListRuns());

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetLatestRun() =>
        FromQuery(service.TryGetLatestRun());

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetRun(
        string runId) =>
        FromQuery(service.TryGetRun(runId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductBatch>> ListBatches(
        string runId) =>
        FromQuery(service.ListBatches(runId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductBatch> GetBatch(
        string runId,
        long providerSequence) =>
        FromQuery(service.TryGetBatch(runId, providerSequence));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductSource>> ListSources(
        string runId) =>
        FromQuery(service.ListSources(runId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductSource> GetSource(
        string runId,
        int sourceId) =>
        FromQuery(service.TryGetSource(runId, sourceId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductHandlerOutput> GetHandlerOutput(
        string runId,
        int sourceId,
        string fieldName) =>
        FromQuery(service.TryGetHandlerOutput(runId, sourceId, fieldName));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductDiagnostics> GetDiagnostics(
        string runId) =>
        FromQuery(service.TryGetDiagnostics(runId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductCapacityEvidence> GetCapacityEvidence(
        string runId) =>
        FromQuery(service.TryGetCapacityEvidence(runId));

    /// <inheritdoc />
    public RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness> GetHistoryReadiness() =>
        RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness>.Ok(
            service.HistoryReadiness);

    /// <inheritdoc />
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
