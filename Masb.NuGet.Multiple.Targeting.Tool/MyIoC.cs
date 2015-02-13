using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    internal static class MyIoC
    {
        public static Task<T> GetAsync<T>()
        {
            return MiniIoC.GetAsync<T>(MiniIoCDefaults.Instance);
        }
    }
}