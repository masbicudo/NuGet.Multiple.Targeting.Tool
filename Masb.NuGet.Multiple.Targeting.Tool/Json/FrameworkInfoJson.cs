using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;
using Masb.NuGet.Multiple.Targeting.Tool.IoC;
using Masb.NuGet.Multiple.Targeting.Tool.Sets;

namespace Masb.NuGet.Multiple.Targeting.Tool.Json
{
    public class FrameworkInfoJson
    {
        public static async Task<FrameworkInfo> ValueAsync(FrameworkInfoJson json)
        {
            if (json == null)
                return null;

            var builder = await MyIoC.GetAsync<FrameworkInfoBuilder>();
            var result = builder.Create(
                json.FrameworkName,
                json.AssemblyInfos
                    .Select(x => AssemblyInfoJson.Value(x.Key, x.Value)),
                json.SupportedFrameworks,
                json.MissingAssemblies);

            return result;
        }

        public static FrameworkInfoJson From(FrameworkInfo obj)
        {
            if (obj == null)
                return null;

            var set = obj.SupportedFrameworks as IntersectionSet<FrameworkName>;
            var list = new List<IUndeterminedSet<FrameworkName>>();
            if (set != null)
            {
                list.AddRange(set.IntersectedSets);
            }
            else
            {
                list.Add(obj.SupportedFrameworks);
            }

            var result = new FrameworkInfoJson
                {
                    AssemblyInfos = obj.AssemblyInfos.ToDictionary(
                        x => x.RelativePath.Substring(2),
                        AssemblyInfoJson.From),
                    FrameworkName = obj.FrameworkName,
                    MissingAssemblies = obj.MissingAssemblies.ToArray(),
                    SupportedFrameworks = list.ToArray()
                };

            return result;
        }

        public FrameworkName FrameworkName { get; set; }

        public Dictionary<string, AssemblyInfoJson> AssemblyInfos { get; set; }

        public IUndeterminedSet<FrameworkName>[] SupportedFrameworks { get; set; }

        public string[] MissingAssemblies { get; set; }
    }
}