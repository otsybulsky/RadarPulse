using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Product;


/// <summary>
/// Product service that composes accepted production-pipeline execution with run history.
/// </summary>
/// <remarks>
/// The service is intentionally a product adapter over existing processing and
/// archive infrastructure. It creates product vocabulary requests, stores the
/// resulting run detail, and exposes query/control helpers without changing the
/// accepted backend runtime semantics.
/// </remarks>
public sealed partial class RadarPulseProductPipelineService : IRadarPulseProductPipelineService
{
    private readonly IRadarPulseProductRunHistoryStore historyStore;
    private readonly RadarProcessingProductionPipelineRunner runner;

    /// <summary>
    /// Creates a service with optional runner and history-store dependencies.
    /// </summary>
    /// <remarks>
    /// The default constructor path uses the accepted production pipeline runner
    /// and process-local in-memory history, which keeps direct tests and CLI
    /// experimentation deterministic.
    /// </remarks>
    public RadarPulseProductPipelineService(
        RadarProcessingProductionPipelineRunner? runner = null,
        IRadarPulseProductRunHistoryStore? historyStore = null)
    {
        this.runner = runner ?? new RadarProcessingProductionPipelineRunner();
        this.historyStore = historyStore ?? new RadarPulseProductInMemoryRunHistoryStore();
    }

    /// <summary>
    /// Creates a service backed by deterministic local file history.
    /// </summary>
    /// <remarks>
    /// This is the accepted local product demo persistence path. It is not a
    /// database-backed or cross-machine history adapter.
    /// </remarks>
    public static RadarPulseProductPipelineService CreateWithFileHistory(
        string historyPath,
        RadarProcessingProductionPipelineRunner? runner = null) =>
        new(
            runner,
            new RadarPulseProductFileRunHistoryStore(historyPath));

    /// <summary>
    /// Number of run details currently visible through the configured history store.
    /// </summary>
    public int Count => historyStore.Count;

    /// <summary>
    /// Current readiness and load posture for the configured history store.
    /// </summary>
    public RadarPulseProductRunHistoryReadiness HistoryReadiness =>
        historyStore.Readiness;

    /// <summary>
    /// Runs the accepted production-shaped pipeline over deterministic synthetic input.
    /// </summary>
    /// <remarks>
    /// This is the primary local demo path. It generates owned archive-shaped
}
