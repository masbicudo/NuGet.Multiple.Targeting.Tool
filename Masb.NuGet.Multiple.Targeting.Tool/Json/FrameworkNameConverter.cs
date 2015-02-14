using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.Json
{
    class FrameworkNameConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            var types = new[]
                {
                    typeof(FrameworkNameSet),
                    typeof(FrameworkFilter),
                    typeof(FrameworkName),
                    typeof(AssemblyName),
                    typeof(IUndeterminedSet<FrameworkName>),
                };

            return types.Contains(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
                return null;

            var strValue = reader.Value.ToString();

            if (typeof(IUndeterminedSet<FrameworkName>).IsAssignableFrom(objectType))
            {
                FrameworkNameSet frmkNameSet;
                if (FrameworkNameSet.TryParse(strValue, out frmkNameSet))
                    return frmkNameSet;

                FrameworkFilter frmkFilter;
                if (FrameworkFilter.TryParse(strValue, out frmkFilter))
                    return frmkFilter;
            }

            if (objectType == typeof(FrameworkName))
            {
                FrameworkName frmkName;
                if (TryParseFrameworkName(strValue, out frmkName))
                    return frmkName;
            }

            if (objectType == typeof(AssemblyName))
            {
                var assemblyName = new AssemblyName(strValue);
                return assemblyName;
            }

            return null;
        }

        public static bool TryParseFrameworkName(string str, out FrameworkName value)
        {
            if (Regex.IsMatch(str, @"^[^,]*,Version=v(?:\d+(?:\.\d+(?:\.\d+(?:\.\d+)?)?)?)(?:,Profile=.*)?$"))
            {
                value = new FrameworkName(str);
                return true;
            }

            value = null;
            return false;
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var frmkName = value as FrameworkName;
            if (frmkName != null)
                writer.WriteValue(frmkName.ToString());

            var frmkNameSet = value as FrameworkNameSet;
            if (frmkNameSet != null)
                writer.WriteValue(frmkNameSet.ToString());

            var frmkNameFilter = value as FrameworkFilter;
            if (frmkNameFilter != null)
                writer.WriteValue(frmkNameFilter.ToString());

            var assemblyName = value as AssemblyName;
            if (assemblyName != null)
                writer.WriteValue(assemblyName.ToString());
        }
    }
}