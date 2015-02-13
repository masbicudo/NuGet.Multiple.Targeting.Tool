using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public interface IFrameworkInfoCache
    {
        Task<FrameworkInfo> GetValueAsync(FrameworkName frameworkName);
        Task SetItemAsync(FrameworkName frameworkName, FrameworkInfo frmkInfo);
    }
}