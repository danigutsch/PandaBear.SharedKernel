using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>Builds a real S3 client and store against a loopback stub and decodes recorded request bodies.</summary>
internal static class RealSdkHarness
{
    /// <summary>Returns the object bytes of a recorded request, decoding aws-chunked framing when present.</summary>
    public static byte[] DecodedBody(RecordedPutRequest request)
    {
        var body = request.Body;
        if (!request.Headers.TryGetValue("Content-Encoding", out var encoding) || !encoding.Contains("aws-chunked", StringComparison.Ordinal))
        {
            request.ContentLength.ShouldBe(body.Length);
            return body;
        }

        using var decoded = new MemoryStream();
        var offset = 0;
        while (true)
        {
            var lineEnd = Array.IndexOf(body, (byte)'\r', offset);
            var header = System.Text.Encoding.ASCII.GetString(body, offset, lineEnd - offset);
            var size = Convert.ToInt32(header.Split(';')[0], 16);
            offset = lineEnd + 2;
            if (size == 0)
            {
                break;
            }

            decoded.Write(body, offset, size);
            offset += size + 2;
        }

        request.Headers["x-amz-decoded-content-length"].ShouldBe(decoded.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return decoded.ToArray();
    }

    /// <summary>Creates a real S3 client pointed at the stub with path-style addressing.</summary>
    public static AmazonS3Client CreateClient(LoopbackS3Stub stub) =>
        new(
            new BasicAWSCredentials("access", "secret"),
            new AmazonS3Config
            {
                ServiceURL = stub.BaseUri.AbsoluteUri,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                MaxErrorRetry = 2
            });

    /// <summary>Creates a store over the client without bucket provisioning.</summary>
    public static S3ObjectStore CreateStore(AmazonS3Client client) =>
        new(client, Options.Create(new S3ObjectStoreOptions
        {
            Endpoint = new Uri("http://127.0.0.1/"),
            PublicBaseUri = new Uri("/cdn/", UriKind.Relative),
            Bucket = "media",
            AccessKey = "access",
            SecretKey = "secret",
            AutoProvisionBucket = false
        }), TimeProvider.System);
}
