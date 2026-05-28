using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;
using RadarPulse.Http.Product;

namespace RadarPulse.Http;

/// <summary>
/// Service registration helpers for the local product HTTP host.
/// </summary>
public static class RadarPulseProductHttpServiceCollectionExtensions
{
    /// <summary>
    /// Named development CORS policy for the Angular operator UI.
    /// </summary>
    public const string OperatorUiCorsPolicyName = "RadarPulseProductOperatorUi";

    /// <summary>
    /// Registers product pipeline API, service, run history, and optional UI CORS services.
    /// </summary>
    /// <remarks>
    /// File-backed history is the default accepted local demo posture. In-memory
    /// history remains available for tests and short-lived local experiments.
    /// </remarks>
    public static IServiceCollection AddRadarPulseProductHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new RadarPulseProductHttpOptions();
        configuration
            .GetSection("RadarPulse:ProductHttp")
            .Bind(options);

        services.AddSingleton(options);
        if (options.EnableOperatorUiCors)
        {
            var origins = options.OperatorUiCorsOrigins
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Where(origin => origin.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (origins.Length > 0)
            {
                services.AddCors(cors => cors.AddPolicy(
                    OperatorUiCorsPolicyName,
                    policy => policy
                        .WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()));
            }
        }

        services.AddSingleton<IRadarPulseProductRunHistoryStore>(
            _ => options.UseInMemoryHistory
                ? new RadarPulseProductInMemoryRunHistoryStore()
                : new RadarPulseProductFileRunHistoryStore(options.HistoryPath));
        services.AddSingleton<RadarPulseProductPipelineService>(
            provider => new RadarPulseProductPipelineService(
                historyStore: provider.GetRequiredService<IRadarPulseProductRunHistoryStore>()));
        services.AddSingleton<IRadarPulseProductPipelineService>(
            provider => provider.GetRequiredService<RadarPulseProductPipelineService>());
        services.AddSingleton<RadarPulseProductPipelineApiContract>();
        services.AddSingleton<IRadarPulseProductPipelineApi>(
            provider => provider.GetRequiredService<RadarPulseProductPipelineApiContract>());
        return services;
    }
}
