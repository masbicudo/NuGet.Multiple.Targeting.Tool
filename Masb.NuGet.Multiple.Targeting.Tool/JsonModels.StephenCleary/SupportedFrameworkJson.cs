namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels.StephenCleary
{
    internal class SupportedFrameworkJson
    {
        public string fullName { get; set; }
        public string displayName { get; set; }

        public override string ToString()
        {
            return this.fullName;
        }
    }
}