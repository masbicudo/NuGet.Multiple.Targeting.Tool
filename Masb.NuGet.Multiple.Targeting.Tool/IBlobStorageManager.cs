using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    /// <summary>
    /// A blob storage manager is a class that can store blobs of information,
    /// that vary from small to huge clusters of information (files for example).
    /// </summary>
    public interface IBlobStorageManager
    {
        /// <summary>
        /// Gets the length of a file if it exists.
        /// Null if the file does not exist.
        /// </summary>
        /// <param name="containerName">Name of the container storing the blob.</param>
        /// <param name="blobName">Name of the blob to get the length of.</param>
        /// <returns>Returns the length of the file if it exists, or null if it does not exist.</returns>
        [CanBeNull]
        Task<long?> GetFileLengthAsync([NotNull] string containerName, [NotNull] string blobName);

        /// <summary>
        /// Uploads a file to the storage.
        /// </summary>
        /// <param name="stream">Stream containing the data to be saved into the file.</param>
        /// <param name="containerName">Name of the container to store the uploaded blob.</param>
        /// <param name="blobName">Name of the blob to save the contents to.</param>
        Task UploadFileToStorageAsync([NotNull] Stream stream, [NotNull] string containerName, [NotNull] string blobName);

        /// <summary>
        /// Downloads a file from the storage.
        /// </summary>
        /// <param name="containerName">Name of the container where the blob resides.</param>
        /// <param name="blobName">Name of the blob to get the contents from.</param>
        /// <returns>Returns a valid stream that can be used to read file data, or null if the file does not exist.</returns>
        [CanBeNull]
        Task<Stream> DownloadFileFromStorageAsync([NotNull] string containerName, [NotNull] string blobName);

        /// <summary>
        /// Deletes a file from the storage.
        /// </summary>
        /// <param name="containerName">Name of the container where the blob to delete is.</param>
        /// <param name="blobName">Name of the blob to delete.</param>
        Task DeleteFileFromStorageAsync([NotNull] string containerName, [NotNull] string blobName);

        /// <summary>
        /// Copies a file stored in storage to another blob.
        /// </summary>
        /// <param name="sourceContainerName">Name of the container where the source blob resides.</param>
        /// <param name="sourceBlobName">Name of the blob to copy data from.</param>
        /// <param name="destinationContainerName">Name of the container where the destination blob will be.</param>
        /// <param name="destinationBlobName">Name of the blob to copy data to.</param>
        Task CopyStoredFileAsync([NotNull] string sourceContainerName, [NotNull] string sourceBlobName, [NotNull] string destinationContainerName, [NotNull] string destinationBlobName);
    }
}