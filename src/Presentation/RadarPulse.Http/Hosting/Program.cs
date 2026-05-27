using RadarPulse.Http;
using RadarPulse.Http.Product;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRadarPulseProductHttp(builder.Configuration);

var app = builder.Build();
var productHttpOptions = app.Services.GetRequiredService<RadarPulseProductHttpOptions>();
if (productHttpOptions.EnableOperatorUiCors &&
    productHttpOptions.OperatorUiCorsOrigins.Any(origin => !string.IsNullOrWhiteSpace(origin)))
{
    app.UseCors(RadarPulseProductHttpServiceCollectionExtensions.OperatorUiCorsPolicyName);
}

app.UseRadarPulseOperatorUiStaticFiles(productHttpOptions);
app.MapRadarPulseProductPipeline();
app.MapRadarPulseOperatorUiFallback(productHttpOptions);
app.Run();

public partial class Program;
