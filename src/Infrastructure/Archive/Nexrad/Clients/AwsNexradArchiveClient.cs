using System.Net;
using System.Xml.Linq;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Historical archive client backed by the public AWS NEXRAD Level II bucket.
/// </summary>
/// <remarks>
/// Listing and download requests use bounded retry for transient HTTP responses while preserving the local
/// historical archive manifest contract.
/// </remarks>
public sealed class AwsNexradArchiveClient(HttpClient httpClient) : IHistoricalArchiveClient
{
    private static readonly XNamespace AwsBucketListNamespace = "http://s3.amazonaws.com/doc/2006-03-01/";
    private const int MaxAttempts = 4;

    /// <inheritdoc />
    public async Task<HistoricalArchiveManifest> BuildManifestAsync(
        HistoricalArchiveRequest request,
        CancellationToken cancellationToken)
    {
        request.ValidateForDiscovery();

        var prefixes = request.AllRadars
            ? [AwsNexradArchiveKey.DatePrefix(request.Date)]
            : request.NormalizedRadarIds.Select(radar => AwsNexradArchiveKey.RadarPrefix(request.Date, radar)).ToArray();

        var files = new List<HistoricalArchiveFile>();
        long totalBytes = 0;
        foreach (var prefix in prefixes)
        {
            await foreach (var file in ListPrefixAsync(prefix, cancellationToken))
            {
                if (request.MaxFiles is { } maxFiles && files.Count >= maxFiles)
                {
                    return new HistoricalArchiveManifest(request.Date, files);
                }

                if (request.MaxBytes is { } maxBytes && totalBytes + file.SizeBytes > maxBytes)
                {
                    return new HistoricalArchiveManifest(request.Date, files);
                }

                files.Add(file);
                totalBytes += file.SizeBytes;
            }
        }

        return new HistoricalArchiveManifest(request.Date, files);
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        HistoricalArchiveFile file,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(destination);

        using var response = await GetWithRetryAsync(BuildObjectUri(file), cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private async IAsyncEnumerable<HistoricalArchiveFile> ListPrefixAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var uri = BuildListUri(prefix, continuationToken);
            using var response = await GetWithRetryAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

            foreach (var content in document.Descendants(AwsBucketListNamespace + "Contents"))
            {
                var key = WebUtility.HtmlDecode(content.Element(AwsBucketListNamespace + "Key")?.Value);
                var sizeText = content.Element(AwsBucketListNamespace + "Size")?.Value;
                var modifiedText = content.Element(AwsBucketListNamespace + "LastModified")?.Value;

                if (key is null ||
                    !long.TryParse(sizeText, out var sizeBytes) ||
                    !DateTimeOffset.TryParse(modifiedText, out var lastModified) ||
                    !AwsNexradArchiveKey.TryParse(key, sizeBytes, lastModified, out var file) ||
                    file is null)
                {
                    continue;
                }

                yield return file;
            }

            continuationToken = document
                .Descendants(AwsBucketListNamespace + "NextContinuationToken")
                .Select(element => element.Value)
                .FirstOrDefault();
        } while (!string.IsNullOrWhiteSpace(continuationToken));
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(string uri, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(uri, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt >= MaxAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
            }

            await Task.Delay(BackoffDelay(attempt), cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static TimeSpan BackoffDelay(int attempt)
    {
        var baseDelayMilliseconds = 200 * Math.Pow(2, attempt - 1);
        var jitterMilliseconds = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds(baseDelayMilliseconds + jitterMilliseconds);
    }

    private static string BuildListUri(string prefix, string? continuationToken)
    {
        var uri = $"https://{AwsNexradArchiveKey.BucketName}.s3.amazonaws.com/?list-type=2&prefix={Uri.EscapeDataString(prefix)}";
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            uri += $"&continuation-token={Uri.EscapeDataString(continuationToken)}";
        }

        return uri;
    }

    private static string BuildObjectUri(HistoricalArchiveFile file) =>
        $"https://{AwsNexradArchiveKey.BucketName}.s3.amazonaws.com/{Uri.EscapeDataString(file.ArchivePath).Replace("%2F", "/")}";
}
