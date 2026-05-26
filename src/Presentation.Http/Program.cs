using RadarPulse.Http;
using RadarPulse.Http.Product;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRadarPulseProductHttp(builder.Configuration);

var app = builder.Build();
app.MapRadarPulseProductPipeline();
app.Run();

public partial class Program;
