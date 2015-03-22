using Masb.NuGet.Multiple.Targeting.Tool.InfoModel;

namespace Masb.NuGet.Multiple.Targeting.Tool.Json
{
    public class TypeSymbolInfoJson
    {
        public static TypeSymbolInfo Value(string name, TypeSymbolInfoJson json)
        {
            if (json == null)
                return null;

            var result = new TypeSymbolInfo(name, json.Variance);
            return result;
        }

        public static TypeSymbolInfoJson From(TypeSymbolInfo obj)
        {
            if (obj == null)
                return null;

            var result = new TypeSymbolInfoJson
                {
                    Variance = obj.Variance
                };

            return result;
        }

        public string[] Variance { get; set; }
    }
}