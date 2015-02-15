namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels.StephenCleary
{
    internal class FrameworkProfileJson
    {
        public string fullName { get; set; }
        public string displayName { get; set; }
        public string profileName { get; set; }
        public bool supportedByVisualStudio2013 { get; set; }
        public bool supportsAsync { get; set; }
        public bool supportsGenericVariance { get; set; }
        public string nugetTarget { get; set; }
        public SupportedFrameworkJson[] frameworks { get; set; }

        public override string ToString()
        {
            return this.fullName;
        }
    }
}