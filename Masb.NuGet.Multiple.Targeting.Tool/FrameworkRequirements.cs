using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Masb.NuGet.Multiple.Targeting.Tool.JsonModels;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class FrameworkRequirements
    {
        /// <summary>
        /// Analyses a framework to tell whether it support or not the requirements.
        /// </summary>
        /// <param name="frameworkInfo">The framework to test.</param>
        /// <returns>True if the framework support the requirements, otherwise False.</returns>
        public async Task<bool> SupportedBy([NotNull] FrameworkInfo frameworkInfo)
        {
            if (frameworkInfo == null)
                throw new ArgumentNullException("frameworkInfo");

            var allTypes = frameworkInfo.AssemblyInfos
                .SelectMany(assembly => assembly.NamedTypes.Select(y => new { assembly, type = y.Value }))
                .GroupBy(x => x.type.TypeName)
                .ToDictionary(x => x.Key, x => x.ToArray());

            var supportedTypes = this.Types
                .Where(tn => tn != "")
                .Where(allTypes.ContainsKey)
                .Select(tn => allTypes[tn])
                .ToList();

            var unsupportedTypes = this.Types
                .Where(tn => tn != "")
                .Where(tn => !allTypes.ContainsKey(tn))
                .ToList();

            var neededAssemblies = supportedTypes
                .SelectMany(x => x.Select(y => y.assembly))
                .Select(a => a.RelativePath)
                .Distinct()
                .ToArray();

            if (unsupportedTypes.Count > 0)
                return false;

            // Setting precompilation symbols according ot the target framework being tested.
            //  (e.g. net40; portable; sl50; etc.)
            // This is needed because the user might already be using these symbols
            // to compensate incompatibilities between frameworks.
            var currentFrameworkName = await this.Project.GetFrameworkName();

            var nugetSymbolsTgt = await NugetSymbols(frameworkInfo.FrameworkName);
            var nugetSymbols = await NugetSymbols(currentFrameworkName);

            var symbols = this.Project.ParseOptions.PreprocessorSymbolNames
                .Concat(nugetSymbolsTgt.nugetSymbols)
                .Except(nugetSymbols.nugetSymbols)
                .Distinct(StringComparer.InvariantCultureIgnoreCase);

            var project = this.Project.WithParseOptions(
                new CSharpParseOptions(preprocessorSymbols: symbols.Concat(new[] { "NUGET_Test" })));
            var compilation = await project.GetCompilationAsync();
            var recompilation = await compilation.RecompileWithReferencesAsync(frameworkInfo, neededAssemblies);

            var diag = recompilation.GetDiagnostics()
                .WhereAsArray(x => !x.IsWarningAsError && x.DefaultSeverity == DiagnosticSeverity.Error);

            // TODO: should errors be ignored exclusivelly through 'NUGET_Test' precompilation symbol?
            // TODO: maybe some of the errors should be ignored, e.g. those not related to the .Net framework
            // TODO: will it be possible to convert projects that don't build (e.g. when frmk is switched no new errors can appear)
            if (diag.Length > 0)
                return false;

            return true;
        }

        private static async Task<NugetSymbolsResult> NugetSymbols(FrameworkName frameworkName)
        {
            var meta = await MetaJson.Load();

            var name = frameworkName.ToString();
            string newName;
            if (meta.aliases.TryGetValue(name, out newName))
                name = newName;

            string nugetName = null;
            IEnumerable<string> nugetSymbols = Enumerable.Empty<string>();

            PortableProfileMetaJson profile;
            if (meta.portableProfiles.TryGetValue(name, out profile))
            {
                nugetName = profile.nugetIds.FirstOrDefault();
                nugetSymbols = new[] { "portable" };
                var addPortableSymbols = profile.supports
                    .SelectMany(
                        name1 =>
                        {
                            IEnumerable<string> names1 = new[] { name1 };

                            names1 = names1
                                .SelectMany<string, string>(
                                    nm =>
                                    {
                                        FilterMetaJson filter1;
                                        if (meta.filters.TryGetValue(nm, out filter1))
                                            return filter1.contains;
                                        return new[] { nm };
                                    });

                            names1 = names1
                                .Select(
                                    nm =>
                                    {
                                        string newName1;
                                        if (meta.aliases.TryGetValue(nm, out newName1))
                                            nm = newName1;
                                        return nm;
                                    });

                            var result1 = names1.SelectMany(nm => meta.frameworks[nm].nugetIds);
                            return result1;
                        });
                nugetSymbols = nugetSymbols.Concat(addPortableSymbols);
            }

            FrameworkMetaJson frmk;
            if (nugetName == null && meta.frameworks.TryGetValue(name, out frmk))
            {
                nugetName = frmk.nugetIds.FirstOrDefault();
                nugetSymbols = meta.frameworks[name].nugetIds;
            }

            NugetSymbolsResult result;
            result.nugetName = nugetName;
            result.nugetSymbols = nugetSymbols.Distinct().ToArray();
            return result;
        }

        private struct NugetSymbolsResult
        {
            public string nugetName;
            public string[] nugetSymbols;
        }

        /// <summary>
        /// Gets or sets information
        /// </summary>
        public string[] Types { get; set; }

        public Project Project { get; set; }
    }
}