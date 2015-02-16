using System.IO;
using System.Threading.Tasks;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class FileHelper
    {
        public static async Task<string> ReadToEndAsync(string fileName)
        {
            using (var stream = new FileStream(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            using (var reader = new StreamReader(stream))
                return await reader.ReadToEndAsync();
        }
    }
}