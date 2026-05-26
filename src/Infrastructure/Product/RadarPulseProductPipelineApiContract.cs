using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed class RadarPulseProductPipelineApiContract
{
    private readonly RadarPulseProductPipelineService service;

    public RadarPulseProductPipelineApiContract(
        RadarPulseProductPipelineService? service = null)
    {
        this.service = service ?? new RadarPulseProductPipelineService();
    }

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

    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>> ListRuns() =>
        RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductRunSummary>>.Ok(service.ListRuns());

    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetLatestRun() =>
        FromQuery(service.TryGetLatestRun());

    public RadarPulseProductApiResponse<RadarPulseProductRunDetail> GetRun(
        string runId) =>
        FromQuery(service.TryGetRun(runId));

    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductBatch>> ListBatches(
        string runId) =>
        FromQuery(service.ListBatches(runId));

    public RadarPulseProductApiResponse<IReadOnlyList<RadarPulseProductSource>> ListSources(
        string runId) =>
        FromQuery(service.ListSources(runId));

    public RadarPulseProductApiResponse<RadarPulseProductDiagnostics> GetDiagnostics(
        string runId) =>
        FromQuery(service.TryGetDiagnostics(runId));

    public RadarPulseProductApiResponse<RadarPulseProductCapacityEvidence> GetCapacityEvidence(
        string runId) =>
        FromQuery(service.TryGetCapacityEvidence(runId));

    public RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness> GetHistoryReadiness() =>
        RadarPulseProductApiResponse<RadarPulseProductRunHistoryReadiness>.Ok(
            service.HistoryReadiness);

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
