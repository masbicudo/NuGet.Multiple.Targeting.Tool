using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Masb.NuGet.Multiple.Targeting.Tool.JsonModels.StephenCleary
{
    internal static class ConvertStephenClearyJsonModel
    {
        public static string ToMetaJson()
        {
            var profiles = PortableProfilesJson.Load();
            var frameworks = MetaJson.Load();

            foreach (var profile in profiles.data)
            {
                PortableProfileMetaJson portableProf;
                if (!frameworks.portableProfiles.TryGetValue(profile.fullName, out portableProf))
                    frameworks.portableProfiles[profile.fullName] = portableProf = new PortableProfileMetaJson();

                portableProf.names = portableProf.names ?? new List<string>();
                portableProf.nugetIds = portableProf.nugetIds ?? new List<string>();
                portableProf.supports = portableProf.supports ?? new List<string>();
                portableProf.vs = portableProf.vs ?? new List<string>();
                portableProf.flags = portableProf.flags ?? new List<string>();

                portableProf.names = portableProf.names.Concat(new[] { profile.displayName }).Distinct().ToList();
                portableProf.nugetIds = portableProf.nugetIds.Concat(new[] { profile.nugetTarget }).Distinct().ToList();
                portableProf.supports = portableProf.supports.Concat(profile.frameworks.Select(x => x.fullName)).Distinct().ToList();
                portableProf.vs = portableProf.vs.Concat(profile.supportedByVisualStudio2013 ? new[] { "12" } : new string[0]).Distinct().ToList();
                portableProf.flags = portableProf.flags.Concat(profile.supportsAsync ? new[] { "async" } : new string[0]).Distinct().ToList();
                portableProf.flags = portableProf.flags.Concat(profile.supportsGenericVariance ? new[] { "variance" } : new string[0]).Distinct().ToList();
            }

            var profilesJson = frameworks.ProfilesToJson(Formatting.None);
            return profilesJson;
        }

    }
}
