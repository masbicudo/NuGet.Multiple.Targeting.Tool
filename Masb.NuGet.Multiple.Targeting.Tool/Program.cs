using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.WaitAll(MainAsync(args));
        }

        private static async Task MainAsync(string[] args)
        {
            var aargs = string.Join(" ", args);

#if DEBUG
            aargs += @" -solution: C:\Projetos\DataStructures\DataStructures.net45.sln";
#endif
            ConsoleHelper.IsActive = true;
            var nodes = await FrameworkInfo.GetFrameworkGraph();

            // looking for all solutions in the current directory
            var slnPathesMatch = Regex.Matches(aargs, @"(?<=\s|^)\-solution:\s+(?:""(?<SLN>[^""]*\.sln)""|(?<SLN>\S*?\.sln))(?=\s+\-|$)").OfType<Match>();
            var slnPathes = slnPathesMatch.Select(m => m.Groups["SLN"].Value).ToArray();
            await AnalyseSolutionsAsync(slnPathes, nodes);
        }

        private static async Task AnalyseSolutionsAsync(IEnumerable<string> slnPathes, HierarchyGraph hierarchyGraph)
        {
            var workspace = MSBuildWorkspace.Create();
            foreach (var slnPath in slnPathes)
            {
                var solution = await workspace.OpenSolutionAsync(slnPath);

                // First thing to do is analyse what .Net profiles may be targeted by this solution.
                // This is done by looking at USED types from the .Net framework.
                // There are 2 kinds of profiles:
                //      ROOT profiles (e.g.: net40; sl4; net45; net45-client; sl5; wp81; wpa81 etc.)
                //      PORTABLE profiles (e.g.: portable-net40+sl4+win+wp7+xbox; portable-net40+sl40+win+wp7)
                // In a first moment, we filter the ROOT profiles by looking .Net types USED by the solution.
                // Rules for filtering profiles given the used type:
                //      1.  profile nativelly support the type          -> keep in the list, without modifications
                //      2.  profile need adapters to support the type   -> keep in the list and add the needed adapter
                //      3.  profile cannot support the type             -> move to the unsupported list
                // We also use the option "support" to operate the set of supported frameworks (from the rules above):
                //      - "support-rem": support PREV except e.g.: "-support-rem: net35, net20, net11, sl40, wp71, wp70"
                //      - "support-add": support PREV and also e.g.: "-support-add: net40, sl50, wpa81"
                //      - "support-exc": support PREV intersecting e.g.: "-support-exc: net45, net40, win, wp81, wpa81"
                //      - "support-eql": support exactly e.g.: "-support-eql: net45, net40, win, wp81, wpa81"
                // With the list of ROOT .Net profiles, we filter the list of PORTABLE profiles.
                // Then we eliminate PORTABLE profiles that are subsets of other PORTABLE profiles,
                // unless the larger set has got adapters that the smaller one does not have, in this case:
                //      1.  use the option "pcl-subset", values:
                //              - "remove" -> always remove the smaller set
                //              - "keep-if-adapters-diff" -> remove the smaller only if ALL adapters are equal
                //      2.  the option can be in: command line argument -or- configuration file
                // We also use the option "unsupported" and "supported" to force the support of portable profiles:
                //      - "support-rem": e.g.: "-support-rem: net35, net20, net11, sl4, wp71, wp7, portable-net40+sl4+win+wp7"
                //      - "support-add": e.g.: "-support-add: net40, sl50, wpa81, portable-net40+sl4+win+wp7+xbox"
                // Finally, we create all the solutions, and try to compile them creating a log file,
                // and also outputing to the console, so that the user can open the solutions and make changes.
                // A "compile all" batch file is created to enable building everything at once.
                // A "make nuget" batch file is created to enable the creation of the nuget package in one step.
                var sortedProject = solution
                    .GetProjectDependencyGraph()
                    .GetTopologicallySortedProjects()
                    .Select(solution.GetProject);

                INamedTypeSymbol[] usedTypes;
                foreach (var project in sortedProject)
                {
                    var compilation = await project.GetCompilationWithReferencesAsync();
                    var diag = compilation.GetDiagnostics();
                    if (diag.Length == 0)
                    {
                        var locator = new UsedTypeLocator(compilation);

                        foreach (var syntaxTree in compilation.SyntaxTrees)
                        {
                            var root = await syntaxTree.GetRootAsync();
                            locator.VisitSyntax(root);
                        }

                        var frameworkRequirements = new FrameworkRequirements
                            {
                                Compilation = compilation,

                                Types = locator.UsedTypes
                                    .Select(RoslynExtensions.GetTypeFullName)
                                    .Where(t => t != "System.Runtime.InteropServices.GuidAttribute")
                                    .Where(t => t != "System.Runtime.InteropServices.ComVisibleAttribute")
                                    .ToArray(),
                            };

                        // determining what frameworks support this set of types
                        var supportedFrameworks = frameworkRequirements.GetSupportGraph(hierarchyGraph);

                        Debugger.Break();
                    }
                }

                workspace.CloseSolution();
            }
        }
    }
}
