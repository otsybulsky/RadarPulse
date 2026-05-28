using System.Text.Json;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductFileRunHistoryStore
{
    private void Persist()
    {
        ThrowIfBlocked();
        CreateParentDirectory();
        var file = new PersistedHistoryFile(
            CurrentSchemaVersion,
            runOrder.Select(runId => runsById[runId]).ToArray());
        var json = JsonSerializer.Serialize(file, JsonOptions);
        var temporaryPath = string.Concat(
            storagePath,
            ".",
            Guid.NewGuid().ToString("N"),
            ".tmp");

        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, storagePath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            firstBlockingReason =
                $"Product run history could not be persisted: {exception.Message}";
            throw new InvalidOperationException(firstBlockingReason, exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void CreateParentDirectory()
    {
        var directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
