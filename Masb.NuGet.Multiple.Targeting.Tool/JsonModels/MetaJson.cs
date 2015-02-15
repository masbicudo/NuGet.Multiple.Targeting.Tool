using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels
{
    internal class MetaJson
    {
        public static MetaJson Load()
        {
            var json = File.ReadAllText("frameworks.json");
            var result = JsonConvert.DeserializeObject<MetaJson>(json);
            return result;
        }

        public string ToJson(Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(this, formatting);
        }

        public string ProfilesToJson(Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(this.portableProfiles, formatting);
        }

        public string description { get; set; }
        public Dictionary<string, FrameworkMetaJson> frameworks { get; set; }
        public Dictionary<string, PortableProfileMetaJson> portableProfiles { get; set; }
        public Dictionary<string, string> aliases { get; set; }
        public Dictionary<string, FilterMetaJson> filters { get; set; }

        public override string ToString()
        {
            return this.description;
        }
    }
}