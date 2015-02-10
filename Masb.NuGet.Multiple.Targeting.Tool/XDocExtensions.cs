using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public static class XDocExtensions
    {
        public static XAttribute Attribute(this XElement element, string name, IEqualityComparer<string> comparer)
        {
            return element.Attributes().SingleOrDefault(
                xa => comparer.Equals(xa.Name.LocalName, name));
        }

        public static XAttribute AttributeI(this XElement element, string name)
        {
            return element.Attributes().SingleOrDefault(
                xa => StringComparer.InvariantCultureIgnoreCase.Equals(xa.Name.LocalName, name));
        }

        public static XElement Element(this XElement element, string name, IEqualityComparer<string> comparer)
        {
            return element.Elements().SingleOrDefault(
                xa => comparer.Equals(xa.Name.LocalName, name));
        }
    }
}