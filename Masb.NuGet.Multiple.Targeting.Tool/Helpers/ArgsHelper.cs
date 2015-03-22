using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masb.NuGet.Multiple.Targeting.Tool.Helpers
{
    public static class ArgsHelper
    {
        private static readonly Dictionary<ArgType, string> dicRgxArg = new Dictionary<ArgType, string>
            {
                { ArgType.Path, @"(?:""(?<PATH>[^""]*)""|(?<PATH>\S*))(?=\s+\-|$)" },
                { ArgType.Text, @"""(?<TEXT>[^""]*)""(?=\s+\-|$)" },
                { ArgType.Integer, @"(?:""(?<PATH>[^""]*\.sln)""|(?<PATH>\S*?\.sln))(?=\s+\-|$)" },
                { ArgType.Decimal, @"(?:""(?<PATH>[^""]*\.sln)""|(?<PATH>\S*?\.sln))(?=\s+\-|$)" },
            };

        private static readonly Dictionary<string, string> dicEscapes = new Dictionary<string, string>
            {
                { "\\n", "\n" },
                { "\\r", "\r" },
                { "\\t", "\t" },
                { "\\0", "\0" },
                { "\\\\", "\\" },
                { "\\\"", "\"" },
                { "\\'", "'" },
            };

        public static object[] ReadArg(string aargs, string argName, ArgType type)
        {
            string rgxArg = dicRgxArg[type];

            var matches =
                Regex.Matches(aargs, String.Format(@"(?<=\s|^)\-{0}:\s*{1}", argName, rgxArg)).OfType<Match>();

            var allValues = matches.Select(m => m.Groups["PATH"].Value)
                .Select(
                    x =>
                    {
                        if (type == ArgType.Path)
                        {
                            return x;
                        }
                        if (type == ArgType.Text)
                        {
                            return Regex.Replace(
                                x,
                                @"\\\.",
                                m =>
                                {
                                    var esc = m.Value;
                                    string value;
                                    if (dicEscapes.TryGetValue(esc, out value))
                                        return value;
                                    return esc;
                                });
                        }
                        else if (type == ArgType.Integer)
                        {
                            int value;
                            if (Int32.TryParse(x, out value))
                                return value;
                        }
                        else if (type == ArgType.Decimal)
                        {
                            decimal value;
                            if (Decimal.TryParse(x, out value))
                                return value;
                        }

                        return null as object;
                    })
                .ToArray();

            return allValues;
        }
    }
}