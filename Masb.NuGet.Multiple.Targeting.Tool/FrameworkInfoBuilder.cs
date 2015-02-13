using System.Collections.Generic;
using System.Runtime.Versioning;
using JetBrains.Annotations;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    /// <summary>
    /// Build a new <see cref="FrameworkInfo"/> class.
    /// </summary>
    public class FrameworkInfoBuilder
    {
        public FrameworkInfoBuilder()
        {
        }

        [NotNull]
        public virtual FrameworkInfo Create(
            [NotNull] FrameworkName frameworkName,
            [NotNull] IEnumerable<AssemblyInfo> assemblyInfos,
            [CanBeNull] IEnumerable<IUndeterminedSet<FrameworkName>> supportedFrameworks,
            [CanBeNull] IEnumerable<string> missingDlls)
        {
            return new FrameworkInfo(
                frameworkName,
                assemblyInfos,
                supportedFrameworks,
                missingDlls);
        }
    }
}