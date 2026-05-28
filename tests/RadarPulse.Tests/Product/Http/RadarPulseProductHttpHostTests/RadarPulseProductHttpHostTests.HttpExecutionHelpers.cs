using System.Text.Json;
using RadarPulse.Application.Product;
using RadarPulse.Http;
using RadarPulse.Http.Product;
using RadarPulse.Infrastructure.Product;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RadarPulse.Tests.Product;

public sealed partial class RadarPulseProductHttpHostTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static async Task<RadarPulseProductApiResponse<T>> ExecuteAsync<T>(
        IResult result)
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        context.RequestServices = services.BuildServiceProvider();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);
        body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<RadarPulseProductApiResponse<T>>(
            body,
            JsonOptions);
        Assert.NotNull(response);
        Assert.Equal(context.Response.StatusCode, response!.StatusCode);
        return response;
    }

    private static DefaultHttpContext CreateHttpContext(
        string path)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(
        DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
