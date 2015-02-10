using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    /// <summary>
    ///  Represents multiple directories at the same time, like an inheritance chain.
    /// </summary>
    /// <remarks>
    /// <para>
    ///  When files are searched, only one file with each name will be returned.
    ///  If the same file exists in more than one node in the chain, the foremost one is returned.
    ///  When directories are searched, if multiple are found, they will form a new DirectoryChain,
    ///      ordered in correspondence with the parents.
    /// </para>
    /// <para>
    ///  This class allows profiles of frameworks, inheriting the parent framework DLLs,
    ///  and other files, but also allowing it to override any file that overlaps.
    /// </para>
    /// </remarks>
    public class DirectoryChain
    {
        private readonly DirectoryInfo[] directoryChain;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryChain"/> class,
        /// passing in a directory chain. Later ones override earlier ones.
        /// </summary>
        /// <param name="directoryChain">
        /// Chain of directories. Later ones override earlier ones.
        /// </param>
        public DirectoryChain([NotNull] params string[] directoryChain)
        {
            if (directoryChain == null)
                throw new ArgumentNullException("directoryChain");

            this.directoryChain = directoryChain.Select(x => new DirectoryInfo(x)).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryChain"/> class,
        /// passing in a directory chain. Later ones override earlier ones.
        /// </summary>
        /// <param name="directoryChain">
        /// Chain of directories. Later ones override earlier ones.
        /// </param>
        public DirectoryChain([NotNull] params DirectoryInfo[] directoryChain)
        {
            if (directoryChain == null)
                throw new ArgumentNullException("directoryChain");

            this.directoryChain = directoryChain;
        }

        /// <summary>
        /// Gets all file in the current directory chain.
        /// </summary>
        /// <returns>An array containing all files in the chain.</returns>
        [NotNull]
        public FileInfo[] GetFiles()
        {
            var result = this.directoryChain
                .SelectMany(x => x.GetFiles())
                .GroupBy(x => x.Name)
                .Select(x => x.Last())
                .ToArray();

            return result;
        }

        /// <summary>
        /// Gets all file that match the search pattern in the current directory chain.
        /// </summary>
        /// <param name="searchPattern">
        /// The search pattern.
        /// </param>
        /// <returns>
        /// An array containing the files in the chain.
        /// </returns>
        [NotNull]
        public FileInfo[] GetFiles([NotNull] string searchPattern)
        {
            if (searchPattern == null)
                throw new ArgumentNullException("searchPattern");

            var result = this.directoryChain
                .SelectMany(x => x.GetFiles(searchPattern))
                .GroupBy(x => x.Name.ToLowerInvariant())
                .Select(x => x.Last())
                .OrderBy(x => x.Name)
                .ToArray();

            return result;
        }

        /// <summary>
        /// Gets all subdirectory chains in the current directory chain.
        /// </summary>
        /// <returns>An array containing all subdirectory chains in this chain.</returns>
        [NotNull]
        public DirectoryChain[] GetDirectories()
        {
            var result = this.directoryChain
                .SelectMany(x => x.GetDirectories())
                .GroupBy(x => x.Name)
                .Select(x => new DirectoryChain(x.ToArray()))
                .ToArray();

            return result;
        }

        /// <summary>
        /// Gets all subdirectory chains matching the search pattern in the current directory chain.
        /// </summary>
        /// <param name="searchPattern">
        /// The search pattern.
        /// </param>
        /// <returns>
        /// An array containing the subdirectory chains in this chain.
        /// </returns>
        [NotNull]
        public DirectoryChain[] GetDirectories([NotNull] string searchPattern)
        {
            if (searchPattern == null)
                throw new ArgumentNullException("searchPattern");

            var result = this.directoryChain
                .SelectMany(x => x.GetDirectories(searchPattern))
                .GroupBy(x => x.Name)
                .Select(x => new DirectoryChain(x.ToArray()))
                .ToArray();

            return result;
        }

        /// <summary>
        /// Gets a subdirectory chain by name in the current directory chain.
        /// </summary>
        /// <param name="directoryName">
        /// The subdirectory chain name to get.
        /// </param>
        /// <returns>
        /// A subdirectory chain with the specified name if found; otherwise null.
        /// </returns>
        [CanBeNull]
        public DirectoryChain GetDirectory([NotNull] string directoryName)
        {
            if (directoryName == null)
                throw new ArgumentNullException("directoryName");

            var result = this.directoryChain
                .Select(x => x.GetDirectories(directoryName).SingleOrDefault())
                .Where(x => x != null)
                .Select(x => new DirectoryChain(x))
                .SingleOrDefault();

            return result;
        }
    }
}