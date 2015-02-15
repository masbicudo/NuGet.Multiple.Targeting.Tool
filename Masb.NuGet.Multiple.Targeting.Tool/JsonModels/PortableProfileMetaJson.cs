using System.Collections.Generic;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels
{
    internal class PortableProfileMetaJson
    {
        public List<string> vs { get; set; }
        public List<string> nugetIds { get; set; }
        public List<string> names { get; set; }
        public List<string> supports { get; set; }
        public List<string> flags { get; set; }

        public string ToJson(Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(this, formatting);
        }
    }
}