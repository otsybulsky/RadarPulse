using System.Text.Json;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductFileRunHistoryStore
{
    private void Load()
    {
        lock (sync)
        {
            if (!File.Exists(storagePath))
            {
                CreateParentDirectory();
                return;
            }

            try
            {
                var json = File.ReadAllText(storagePath);
                var file = JsonSerializer.Deserialize<PersistedHistoryFile>(json, JsonOptions);
                if (file is null)
                {
                    Block("Product run history file is empty or invalid.", rejectedCount: 1);
                    return;
                }

                if (file.SchemaVersion != CurrentSchemaVersion)
                {
                    Block(
                        $"Product run history schema version {file.SchemaVersion} is not supported.",
                        rejectedCount: Math.Max(file.Runs?.Count ?? 1, 1));
                    return;
                }

                if (file.Runs is null)
                {
                    Block("Product run history file does not contain a runs collection.", rejectedCount: 1);
                    return;
                }

                foreach (var detail in file.Runs)
                {
                    if (detail is null || string.IsNullOrWhiteSpace(detail.RunId))
                    {
                        Block("Product run history contains an invalid run record.", rejectedCount: 1);
                        return;
                    }

                    if (runsById.TryGetValue(detail.RunId, out var existing))
                    {
                        if (AreSameRecord(existing, detail))
                        {
                            continue;
                        }

                        Block(
                            $"Product run history contains conflicting duplicate run '{detail.RunId}'.",
                            rejectedCount: 1);
                        return;
                    }

                    runsById.Add(detail.RunId, detail);
                    runOrder.Add(detail.RunId);
                }
            }
            catch (JsonException exception)
            {
                Block(
                    $"Product run history JSON is invalid: {exception.Message}",
                    rejectedCount: 1);
            }
            catch (IOException exception)
            {
                Block(
                    $"Product run history could not be loaded: {exception.Message}",
                    rejectedCount: 1);
            }
            catch (UnauthorizedAccessException exception)
            {
                Block(
                    $"Product run history could not be accessed: {exception.Message}",
                    rejectedCount: 1);
            }
        }
    }
}
