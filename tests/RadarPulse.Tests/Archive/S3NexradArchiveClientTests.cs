using System.Net;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class S3NexradArchiveClientTests
{
    [Fact]
    public async Task BuildManifestAsyncStopsWhenMaxBytesWouldBeExceeded()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(ListResponse(
            """
            <Contents>
              <Key>2026/05/04/KTLX/KTLX20260504_000245_V06</Key>
              <LastModified>2026-05-04T00:09:43.000Z</LastModified>
              <Size>1000</Size>
            </Contents>
            <Contents>
              <Key>2026/05/04/KTLX/KTLX20260504_000745_V06</Key>
              <LastModified>2026-05-04T00:14:43.000Z</LastModified>
              <Size>1000</Size>
            </Contents>
            """)));
        var client = new S3NexradArchiveClient(httpClient);
        var request = new HistoricalArchiveRequest(
            new DateOnly(2026, 5, 4),
            RadarIds: ["KTLX"],
            MaxBytes: 1_500);

        var manifest = await client.BuildManifestAsync(request, CancellationToken.None);

        var file = Assert.Single(manifest.Files);
        Assert.Equal(1_000, file.SizeBytes);
    }

    [Fact]
    public async Task BuildManifestAsyncRetriesTransientListingFailures()
    {
        var handler = new SequenceResponseHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ListResponse(
                    """
                    <Contents>
                      <Key>2026/05/04/KTLX/KTLX20260504_000245_V06</Key>
                      <LastModified>2026-05-04T00:09:43.000Z</LastModified>
                      <Size>1000</Size>
                    </Contents>
                    """))
            });
        using var httpClient = new HttpClient(handler);
        var client = new S3NexradArchiveClient(httpClient);
        var request = new HistoricalArchiveRequest(
            new DateOnly(2026, 5, 4),
            RadarIds: ["KTLX"],
            MaxFiles: 1);

        var manifest = await client.BuildManifestAsync(request, CancellationToken.None);

        Assert.Single(manifest.Files);
        Assert.Equal(2, handler.RequestCount);
    }

    private static string ListResponse(string contents) =>
        $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
          <Name>unidata-nexrad-level2</Name>
          <Prefix>2026/05/04/KTLX/</Prefix>
          <KeyCount>1</KeyCount>
          <MaxKeys>1000</MaxKeys>
          <IsTruncated>false</IsTruncated>
          {{contents}}
        </ListBucketResult>
        """;

    private sealed class StaticResponseHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
    }

    private sealed class SequenceResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
