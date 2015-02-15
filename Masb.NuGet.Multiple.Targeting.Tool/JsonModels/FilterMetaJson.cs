using System.Collections.Generic;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels
{
    internal class FilterMetaJson
    {
        public List<string> contains { get; set; }
        public List<string> names { get; set; }

        public string ToJson(Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(this, formatting);
        }
    }
}
