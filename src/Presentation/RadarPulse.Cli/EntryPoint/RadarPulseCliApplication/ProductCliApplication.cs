using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal static class ProductCliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "pipeline" => await ProductPipelineAsync(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }

    static async Task<int> ProductPipelineAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "demo" => await ProductPipelineCliWorkflow.RunDemoAsync(args[1..]),
            "run-archive" => await ProductPipelineCliWorkflow.RunArchiveAsync(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }

}
