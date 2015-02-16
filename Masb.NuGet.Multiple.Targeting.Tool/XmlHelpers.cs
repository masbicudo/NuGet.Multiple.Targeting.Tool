using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    internal static class XmlHelpers
    {
        public static async Task<XDocument> ReadXDocumentAsync(string xmlFileName)
        {
            var xml = await FileHelper.ReadToEndAsync(xmlFileName);
            return XDocument.Load(new StringReader(xml));
        }

        public static async Task<T> DesserializeAsync<T>(string xmlFileName)
        {
            var xml = await FileHelper.ReadToEndAsync(xmlFileName);
            var xmlSer = new XmlSerializer(typeof(T));
            var r = (T)xmlSer.Deserialize(new StringReader(xml));
            return r;
        }
    }
}