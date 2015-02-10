using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    internal static class XmlHelpers
    {
        public static async Task<XDocument> ReadXml(string xmlFileName)
        {
            using (var stream = new FileStream(
                xmlFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            {
                using (var reader = new StreamReader(stream))
                {
                    return XDocument.Load(await reader.ReadToEndAsync());
                }
            }
        }

        public static async Task<T> DesserializeAsync<T>(string xmlFileName)
        {
            using (var stream = new FileStream(
                xmlFileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true))
            {
                using (var reader = new StreamReader(stream))
                {
                    var text = await reader.ReadToEndAsync();
                    var xmlSer = new XmlSerializer(typeof(T));
                    var r = (T)xmlSer.Deserialize(new StringReader(text));
                    return r;
                }
            }
        }
    }
}