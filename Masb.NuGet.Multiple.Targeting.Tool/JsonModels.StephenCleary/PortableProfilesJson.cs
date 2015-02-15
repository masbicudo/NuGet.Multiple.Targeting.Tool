using System.IO;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels.StephenCleary
{
    internal class PortableProfilesJson
    {
        // http://blog.stephencleary.com/2012/05/framework-profiles-in-net.html
        public static PortableProfilesJson Load()
        {
            var json = File.ReadAllText("profiles.json");
            var result = JsonConvert.DeserializeObject<PortableProfilesJson>(json);
            return result;
        }

        public string description { get; set; }
        public FrameworkProfileJson[] data { get; set; }

        public override string ToString()
        {
            return this.description;
        }
    }
}
