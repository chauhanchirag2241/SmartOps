using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SmartOps.Application.Abstractions.Storage;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a file to Blob Storage and returns the public URL.
    /// </summary>
    Task<string> UploadFileAsync(string containerName, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from Blob Storage.
    /// </summary>
    Task<bool> DeleteFileAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
}
