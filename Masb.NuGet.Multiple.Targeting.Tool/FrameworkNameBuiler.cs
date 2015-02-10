using System;
using System.Runtime.Versioning;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    internal class FrameworkNameBuiler
    {
        public FrameworkNameBuiler()
        {
        }

        public FrameworkNameBuiler(string identifier)
        {
            this.Identifier = identifier;
        }

        public FrameworkNameBuiler(string identifier, string version)
        {
            this.Identifier = identifier;
            this.Version = version;
        }

        public FrameworkNameBuiler(string identifier, string version, string profile)
        {
            this.Identifier = identifier;
            this.Version = version;
            this.Profile = profile;
        }

        public string Identifier { get; set; }

        public string Version { get; set; }

        public string Profile { get; set; }

        public FrameworkNameBuiler Clone()
        {
            return new FrameworkNameBuiler
                {
                    Identifier = this.Identifier,
                    Version = this.Version,
                    Profile = this.Profile,
                };
        }

        public FrameworkName ToFrameworkName()
        {
            return new FrameworkName(this.Identifier, new Version(this.Version), this.Profile);
        }
    }
}