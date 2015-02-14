using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FileSystemBlobStorageManager : IBlobStorageManager
    {
        private readonly string baseDirectory;

        protected FileSystemBlobStorageManager()
        {
        }

        public FileSystemBlobStorageManager(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
        }

        public virtual string BaseDirectory
        {
            get { return this.baseDirectory; }
        }

        /// <summary>
        /// Gets the length of a file if it exists.
        /// Null if the file does not exist.
        /// </summary>
        /// <param name="containerName">Name of the container storing the blob.</param>
        /// <param name="blobName">Name of the blob to get the length of.</param>
        /// <returns>Returns a task of reading the length of the file if it exists, or null if it does not exist.</returns>
        public async Task<long?> GetFileLengthAsync(string containerName, string blobName)
        {
            var localPath = Path.Combine(this.BaseDirectory, containerName, blobName);
            var fileInfo = new FileInfo(localPath);
            if (fileInfo.Exists)
                return fileInfo.Length;

            return null;
        }

        /// <summary>
        /// Uploads a file to the file system.
        /// </summary>
        /// <param name="stream">Stream containing the data to be saved into the file.</param>
        /// <param name="containerName">Name of the container to store the uploaded blob.</param>
        /// <param name="blobName">Name of the blob to save the contents to.</param>
        /// <returns>A task of uploading a file to the storage.</returns>
        public async Task UploadFileToStorageAsync(
            [NotNull] Stream stream,
            [NotNull] string containerName,
            [NotNull] string blobName)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (containerName == null)
                throw new ArgumentNullException("containerName");
            if (blobName == null)
                throw new ArgumentNullException("blobName");

            // if container already exists, just create the directories inside
            var filePath = Path.Combine(this.BaseDirectory, containerName, blobName);

            var destDir = Path.GetDirectoryName(filePath);
            if (destDir != null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            FileStream fs = null;
            for (int it = 0; it < 10; it++)
            {
                if (it > 0)
                    await Task.Delay(1000);

                fs = DirPathExistsResult(
                    destDir,
                    () => new FileStream(
                        filePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        useAsync: true),
                    null);

                if (fs != null)
                    break;
            }

            if (fs == null)
                throw new System.IO.IOException("Coult not open file for writing.");

            using (fs)
                await stream.CopyToAsync(fs);
        }

        /// <summary>
        /// Downloads a file from the file system.
        /// </summary>
        /// <param name="containerName">Name of the container where the blob resides.</param>
        /// <param name="blobName">Name of the blob to get the contents from.</param>
        /// <returns>Returns a valid stream that can be used to read file data, or null if the file does not exist.</returns>
        /// <returns>A task of getting a stream to download data.</returns>
        public async Task<Stream> DownloadFileFromStorageAsync(string containerName, string blobName)
        {
            var sourcePath = Path.Combine(this.BaseDirectory, containerName, blobName);
            var result = FileExistsResult(
                sourcePath,
                () => new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true),
                null);
            return result;
        }

        /// <summary>
        /// Deletes a file from the file system.
        /// </summary>
        /// <param name="containerName">Name of the container where the blob to delete is.</param>
        /// <param name="blobName">Name of the blob to delete.</param>
        /// <returns>A task of deleting the file.</returns>
        public async Task DeleteFileFromStorageAsync(string containerName, string blobName)
        {
            var pathContainer = Path.Combine(this.BaseDirectory, containerName) + '\\';
            var path = Path.Combine(this.BaseDirectory, containerName, blobName);
            var dirInfo = new DirectoryInfo(path);
            var fileInfo = new FileInfo(path);

            while (true)
            {
                // ReSharper disable AccessToModifiedClosure
                if (pathContainer.StartsWith(dirInfo.FullName))
                    break;

                if (fileInfo != null && FileInfoExistsAction(fileInfo, fileInfo.Delete))
                {
                    dirInfo = fileInfo.Directory;
                    fileInfo = null;
                }
                else
                {
                    DirInfoExistsAction(dirInfo, () => dirInfo.Delete(true));
                    dirInfo = dirInfo.Parent;
                }

                if (dirInfo == null)
                    break;

                if (DirInfoExistsResult(
                    dirInfo,
                    () => Directory.EnumerateFileSystemEntries(dirInfo.FullName).Any(),
                    false))
                    break;

                // ReSharper restore AccessToModifiedClosure
            }
        }

        public async Task<bool> InternalCopyFileAsync(BlobLocation fileLocation, BlobLocation destinationFileLocation)
        {
            var sourcePath = Path.Combine(this.BaseDirectory, fileLocation.FullName);
            var destinationPath = Path.Combine(this.BaseDirectory, destinationFileLocation.FullName);

            var destDir = Path.GetDirectoryName(destinationPath);
            if (destDir != null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            var result = FilePathExistsAction(sourcePath, () => File.Copy(sourcePath, destinationPath));
            return result;
        }

        /// <summary>
        /// Copies a file stored in file system to another blob.
        /// </summary>
        /// <param name="sourceContainerName">Name of the container where the source blob resides.</param>
        /// <param name="sourceBlobName">Name of the blob to copy data from.</param>
        /// <param name="destinationContainerName">Name of the container where the destination blob will be.</param>
        /// <param name="destinationBlobName">Name of the blob to copy data to.</param>
        /// <returns>A task representing the intended action.</returns>
        public async Task CopyStoredFileAsync(
            [NotNull] string sourceContainerName,
            [NotNull] string sourceBlobName,
            [NotNull] string destinationContainerName,
            [NotNull] string destinationBlobName)
        {
            if (sourceContainerName == null)
                throw new ArgumentNullException("sourceContainerName");

            if (sourceBlobName == null)
                throw new ArgumentNullException("sourceBlobName");

            if (destinationContainerName == null)
                throw new ArgumentNullException("destinationContainerName");

            if (destinationBlobName == null)
                throw new ArgumentNullException("destinationBlobName");

            var srcLocation = new BlobLocation(sourceContainerName, sourceBlobName);
            var dstLocation = new BlobLocation(destinationContainerName, destinationBlobName);

            await this.InternalCopyFileAsync(srcLocation, dstLocation);
        }

        private static bool FilePathExistsAction(string path, Action action)
        {
            if (!DEBUG || File.Exists(path))
                try
                {
                    action();
                    return true;
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

            return false;
        }

        private static bool FileInfoExistsAction(FileInfo fileInfo, Action action)
        {
            if (!DEBUG || fileInfo.Exists)
                try
                {
                    action();
                    return true;
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

            return false;
        }

        private static void DirInfoExistsAction(DirectoryInfo dirInfo, Action action)
        {
            if (!DEBUG || dirInfo.Exists)
                try
                {
                    action();
                }
                catch (DirectoryNotFoundException)
                {
                }
        }

        private static T DirInfoExistsResult<T>(DirectoryInfo dirInfo, Func<T> func, T notExists)
        {
            if (!DEBUG || dirInfo.Exists)
                try
                {
                    return func();
                }
                catch (DirectoryNotFoundException)
                {
                }

            return notExists;
        }

        private static T DirPathExistsResult<T>(string path, Func<T> func, T notExists)
        {
            if (!DEBUG || Directory.Exists(path))
                try
                {
                    return func();
                }
                catch (DirectoryNotFoundException)
                {
                }

            return notExists;
        }

        private static T FileExistsResult<T>(string path, Func<T> func, T notExists)
        {
            if (!DEBUG || File.Exists(path))
                try
                {
                    return func();
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

            return notExists;
        }

#if DEBUG
        private const bool DEBUG = true;
#else
        const bool DEBUG = false;
#endif
    }
}
