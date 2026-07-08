using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SmartOps.Application.Abstractions.Storage;

namespace SmartOps.Infrastructure.Storage;

public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(IOptions<BlobStorageOptions> options)
    {
        _blobServiceClient = new BlobServiceClient(options.Value.ConnectionString);
    }

    public async Task<string> UploadFileAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);
        
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blobClient.UploadAsync(content, options, cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteFileAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var result = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return result.Value;
    }
}
