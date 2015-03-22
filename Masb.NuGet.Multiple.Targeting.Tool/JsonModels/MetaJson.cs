using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.Helpers;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels
{
    internal class MetaJson
    {
        public MetaJson()
        {
            this.frameworks = new Dictionary<string, FrameworkMetaJson>(StringComparer.InvariantCultureIgnoreCase);
            this.portableProfiles = new Dictionary<string, PortableProfileMetaJson>(StringComparer.InvariantCultureIgnoreCase);
            this.aliases = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            this.filters = new Dictionary<string, FilterMetaJson>(StringComparer.InvariantCultureIgnoreCase);
        }

        private static MetaJson cache;
        private static readonly AsyncLock locker = new AsyncLock();
        public static async Task<MetaJson> Load()
        {
            if (cache == null)
                using (await locker)
                    if (cache == null)
                    {
                        var json = await FileHelper.ReadToEndAsync("frameworks.json");
                        cache = JsonConvert.DeserializeObject<MetaJson>(json);
                    }

            return cache;
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
        public Dictionary<string, FrameworkMetaJson> frameworks { get; private set; }
        public Dictionary<string, PortableProfileMetaJson> portableProfiles { get; private set; }
        public Dictionary<string, string> aliases { get; private set; }
        public Dictionary<string, FilterMetaJson> filters { get; private set; }

        public override string ToString()
        {
            return this.description;
        }
    }
}