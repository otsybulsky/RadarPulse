using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductFileRunHistoryStore
{
    /// <summary>
    /// Persists a new product run detail unless a conflicting run id already exists.
    /// </summary>
    public void Store(
        RadarPulseProductRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail.RunId);

        lock (sync)
        {
            ThrowIfBlocked();
            if (runsById.TryGetValue(detail.RunId, out var existing))
            {
                if (AreSameRecord(existing, detail))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Product run history already contains conflicting run '{detail.RunId}'.");
            }

            runsById.Add(detail.RunId, detail);
            runOrder.Add(detail.RunId);
            try
            {
                Persist();
            }
            catch
            {
                runsById.Remove(detail.RunId);
                runOrder.RemoveAt(runOrder.Count - 1);
                throw;
            }
        }
    }
}
