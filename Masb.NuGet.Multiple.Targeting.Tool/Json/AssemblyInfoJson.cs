using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;

namespace Masb.NuGet.Multiple.Targeting.Tool.Json
{
    public class AssemblyInfoJson
    {
        public static AssemblyInfo Value(string name, AssemblyInfoJson json)
        {
            if (json == null)
                return null;

            var result = new AssemblyInfo(
                "~\\" + name,
                json.AssemblyName,
                json.Types.Select(x => TypeSymbolInfoJson.Value(x.Key, x.Value)));

            return result;
        }

        public static AssemblyInfoJson From(AssemblyInfo obj)
        {
            if (obj == null)
                return null;

            var result = new AssemblyInfoJson
                {
                    AssemblyName = obj.AssemblyName,
                    Types = obj.NamedTypes.Select(x => x.Value).ToDictionary(
                        x => x.TypeName,
                        TypeSymbolInfoJson.From)
                };

            return result;
        }

        public AssemblyName AssemblyName { get; set; }

        public Dictionary<string, TypeSymbolInfoJson> Types { get; set; }
    }
}