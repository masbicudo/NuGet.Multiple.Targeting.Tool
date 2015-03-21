using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Masb.NuGet.Multiple.Targeting.Tool.JsonModels;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.SetBufferSize(180, 8000);
            Console.SetWindowSize(Console.LargestWindowWidth * 9 / 10, Console.LargestWindowHeight * 9 / 10);
            Console.SetWindowPosition(0, 0);
            Task.WaitAll(MainAsync(args));
        }

        private static async Task MainAsync(string[] args)
        {
            var aargs = string.Join(" ", args);

#if DEBUG
            aargs += @" -solution: C:\Projetos\DataStructures\DataStructures.net45.sln";
#endif
            // initializing components
            await MiniIoC.RegisterAsync<IFrameworkInfoCache>(c => new StorageFrameworkInfoCache(c.Get<IBlobStorageManager>()));
            await MiniIoC.RegisterAsync<IBlobStorageManager>(c => new AppDataBlobStorageManager());

            ConsoleHelper.IsActive = true;
            var nodes = await FrameworkInfo.GetFrameworkGraph();

            // looking for all solutions in the current directory
            var slnPathes = ArgsHelper.ReadArg(aargs, "solution", ArgType.Path).OfType<string>().ToArray();
            if (slnPathes.Length > 0)
                await AnalyseSolutionsAsync(slnPathes, nodes);
        }


        private static async Task AnalyseSolutionsAsync(IEnumerable<string> slnPathes, HierarchyGraph hierarchyGraph)
        {
            var workspace = MSBuildWorkspace.Create();
            foreach (var slnPath in slnPathes)
            {
                if (!File.Exists(slnPath))
                    throw new FileNotFoundException("Solution file does not exist:\n - " + slnPath);

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

                foreach (var project in sortedProject)
                {
                    ConsoleHelper.WriteLine("Processing project: " + PathHelper.GetRelativePath(solution.FilePath, project.FilePath), ConsoleColor.DarkMagenta);

                    var project2 = project;

                    // if we skip this line the project comes without references,
                    // causing thousands of reference errors, when `GetDiagnostics` is called below
                    project2 = await project2.GetProjectWithReferencesAsync();

                    var compilation = await project2.GetCompilationAsync();
                    var diag = compilation.GetDiagnostics()
                        .WhereAsArray(x => !x.IsWarningAsError && x.DefaultSeverity == DiagnosticSeverity.Error);

                    if (diag.Length == 0)
                    {
                        var locator = new UsedTypeLocator(compilation);

                        foreach (var syntaxTree in compilation.SyntaxTrees)
                        {
                            ConsoleHelper.WriteLine(
                                "Loading file: " + PathHelper.GetRelativePath(solution.FilePath, syntaxTree.FilePath),
                                ConsoleColor.Magenta,
                                1);

                            var root = await syntaxTree.GetRootAsync();
                            locator.VisitSyntax(root);
                        }

                        var frameworkRequirements = new FrameworkRequirements
                            {
                                Project = project,

                                Types = locator.UsedTypes
                                    .Select(RoslynExtensions.GetTypeFullName)
                                    .Where(t => t != "System.Runtime.InteropServices.GuidAttribute")
                                    .Where(t => t != "System.Runtime.InteropServices.ComVisibleAttribute")
                                    .ToArray(),
                            };

                        // determining what frameworks support this set of types
                        var supportedFrameworks = await frameworkRequirements.GetSupportGraphAsync(hierarchyGraph);

                        ConsoleHelper.WriteLine();
                        ConsoleHelper.Write("LIST OF FRAMEWORKS THAT SUPPORT: ", ConsoleColor.Yellow);
                        ConsoleHelper.WriteLine(project2.FilePath, ConsoleColor.Blue);
                        ConsoleHelper.WriteLine();

                        var meta = await MetaJson.Load();

                        var dicPossibleChoices = new Dictionary<string, SupportGraph>(StringComparer.InvariantCultureIgnoreCase);
                        foreach (var node in supportedFrameworks)
                            node.Visit(
                                path =>
                                {
                                    var name = path.Peek().ToString();

                                    string newName;
                                    if (meta.aliases.TryGetValue(name, out newName))
                                        name = newName;

                                    string acronym = null;

                                    PortableProfileMetaJson portable;
                                    if (meta.portableProfiles.TryGetValue(name, out portable))
                                        acronym = new FrameworkName(name).Profile;

                                    FrameworkMetaJson frmk;
                                    if (meta.frameworks.TryGetValue(name, out frmk))
                                        acronym = frmk.nugetIds.FirstOrDefault();

                                    if (acronym != null)
                                        dicPossibleChoices.Add(acronym, path.Peek());

                                    ConsoleHelper.Write(name, acronym == null ? ConsoleColor.DarkGray : ConsoleColor.White, path.Count());
                                    if (acronym != null)
                                    {
                                        ConsoleHelper.Write(" ( ", ConsoleColor.DarkCyan);
                                        ConsoleHelper.Write(acronym, ConsoleColor.Cyan);
                                        ConsoleHelper.Write(" ) ", ConsoleColor.DarkCyan);
                                    }

                                    ConsoleHelper.WriteLine();
                                });

                        ConsoleHelper.WriteLine();
                        ConsoleHelper.WriteLine("Select the frameworks to use:", ConsoleColor.Gray);
                        var chosen = Console.ReadLine();
                        if (chosen != null)
                        {
                            var choices = chosen.Split(@",; \/|+-_".ToCharArray());

                            // we need to 
                        }

                    }
                }

                workspace.CloseSolution();
            }
        }
    }
}
