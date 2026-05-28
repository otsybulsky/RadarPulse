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
    private sealed class OperatorUiStaticRoot :
        IDisposable
    {
        private OperatorUiStaticRoot(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static OperatorUiStaticRoot Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"radarpulse-operator-ui-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            File.WriteAllText(
                System.IO.Path.Combine(path, "index.html"),
                "<!doctype html><html><body>operator-ui-shell</body></html>");
            File.WriteAllText(
                System.IO.Path.Combine(path, "main.js"),
                "console.log('operator ui');");

            return new OperatorUiStaticRoot(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TemporaryDirectory :
        IDisposable
    {
        private TemporaryDirectory(
            string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"radarpulse-history-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class RouteBuilderStub :
        IEndpointRouteBuilder
    {
        public RouteBuilderStub(
            IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            DataSources = new List<EndpointDataSource>();
        }

        public IServiceProvider ServiceProvider { get; }

        public ICollection<EndpointDataSource> DataSources { get; }

        public IApplicationBuilder CreateApplicationBuilder() =>
            throw new NotSupportedException();
    }
}
