using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class MultiDirectoryInfo
    {
        private readonly DirectoryInfo[] directories;

        public MultiDirectoryInfo([NotNull] params string[] directories)
        {
            if (directories == null)
                throw new ArgumentNullException("directories");

            this.directories = directories.Select(x => new DirectoryInfo(x)).ToArray();
        }

        public MultiDirectoryInfo([NotNull] params DirectoryInfo[] directories)
        {
            if (directories == null)
                throw new ArgumentNullException("directories");

            this.directories = directories;
        }

        [NotNull]
        public FileInfo[] GetFiles()
        {
            var result = this.directories
                .SelectMany(x => x.GetFiles())
                .GroupBy(x => x.Name)
                .Select(x => x.Last())
                .ToArray();

            return result;
        }

        [NotNull]
        public FileInfo[] GetFiles([NotNull] string searchPattern)
        {
            if (searchPattern == null)
                throw new ArgumentNullException("searchPattern");

            var result = this.directories
                .SelectMany(x => x.GetFiles(searchPattern))
                .GroupBy(x => x.Name)
                .Select(x => x.Last())
                .ToArray();

            return result;
        }

        [NotNull]
        public MultiDirectoryInfo[] GetDirectories()
        {
            var result = this.directories
                .SelectMany(x => x.GetDirectories())
                .GroupBy(x => x.Name)
                .Select(x => new MultiDirectoryInfo(x.ToArray()))
                .ToArray();

            return result;
        }

        [NotNull]
        public MultiDirectoryInfo[] GetDirectories([NotNull] string searchPattern)
        {
            if (searchPattern == null)
                throw new ArgumentNullException("searchPattern");

            var result = this.directories
                .SelectMany(x => x.GetDirectories(searchPattern))
                .GroupBy(x => x.Name)
                .Select(x => new MultiDirectoryInfo(x.ToArray()))
                .ToArray();

            return result;
        }

        [CanBeNull]
        public MultiDirectoryInfo GetDirectory([NotNull] string directoryName)
        {
            if (directoryName == null)
                throw new ArgumentNullException("directoryName");

            var result = this.directories
                .Select(x => x.GetDirectories(directoryName).SingleOrDefault())
                .Where(x => x != null)
                .Select(x => new MultiDirectoryInfo(x))
                .SingleOrDefault();

            return result;
        }
    }
}