internal static class RadarPulseCliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return RadarPulseCliUsage.Print();
        }

        try
        {
            return args[0] switch
            {
                "archive" => await ArchiveCliApplication.RunAsync(args[1..]),
                "processing" => ProcessingCliApplication.Run(args[1..]),
                "product" => await ProductCliApplication.RunAsync(args[1..]),
                _ => RadarPulseCliUsage.Print()
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 1;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or IOException or InvalidDataException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
