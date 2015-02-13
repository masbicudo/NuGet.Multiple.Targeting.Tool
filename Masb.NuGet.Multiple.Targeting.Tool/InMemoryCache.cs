using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class InMemoryCache : IFrameworkInfoCache
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
