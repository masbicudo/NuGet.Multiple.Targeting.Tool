using System.Collections.Immutable;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.Helpers;

namespace Masb.NuGet.Multiple.Targeting.Tool.InfoModel
{
    public class InMemoryFrameworkInfoCache : IFrameworkInfoCache
    {
        private static readonly AsyncLock cacheLocker = new AsyncLock();

        private static ImmutableDictionary<FrameworkName, FrameworkInfo> frmkInfoCache =
            ImmutableDictionary<FrameworkName, FrameworkInfo>.Empty;

        public async Task<FrameworkInfo> GetValueAsync(FrameworkName frameworkName)
        {
            FrameworkInfo value;
            if (frmkInfoCache.TryGetValue(frameworkName, out value))
                return value;

            return null;
        }

        public async Task SetItemAsync(FrameworkName frameworkName, FrameworkInfo frmkInfo)
        {
            using (await cacheLocker)
                frmkInfoCache = frmkInfoCache.SetItem(frameworkName, frmkInfo);
        }
    }
}
