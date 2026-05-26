using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;
using RadarPulse.Http.Product;

namespace RadarPulse.Http;

public static class RadarPulseProductHttpServiceCollectionExtensions
{
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
        services.AddSingleton<IRadarPulseProductRunHistoryStore>(
            _ => options.UseInMemoryHistory
                ? new RadarPulseProductInMemoryRunHistoryStore()
                : new RadarPulseProductFileRunHistoryStore(options.HistoryPath));
        services.AddSingleton<RadarPulseProductPipelineService>(
            provider => new RadarPulseProductPipelineService(
                historyStore: provider.GetRequiredService<IRadarPulseProductRunHistoryStore>()));
        services.AddSingleton<RadarPulseProductPipelineApiContract>();
        return services;
    }
}
