using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Masb.NuGet.Multiple.Targeting.Tool.Graphs;
using Masb.NuGet.Multiple.Targeting.Tool.Helpers;
using Masb.NuGet.Multiple.Targeting.Tool.RoslynExtensions;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class ProjectAnalysis
    {
        [NotNull]
        public Project Project { get; private set; }

        [NotNull]
        public ProjectSupportGraph[] SupportedFrameworks { get; private set; }

        private ProjectAnalysis([NotNull] Project project, [NotNull] ProjectSupportGraph[] supportedFrameworks)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            if (supportedFrameworks == null)
                throw new ArgumentNullException("supportedFrameworks");

            this.Project = project;
            this.SupportedFrameworks = supportedFrameworks;
        }

        public static async Task<ProjectAnalysis> AnalyseProjectAsync(
            Project project,
            HierarchyGraph hierarchyGraph,
            ProjectAnalysis[] dependencies)
        {
            var solution = project.Solution;
            ConsoleHelper.WriteLine(
                "Processing project: " + PathHelper.GetRelativePath(solution.FilePath, project.FilePath),
                ConsoleColor.DarkMagenta);

            var project2 = project;

            // if we skip this line the project comes without references,
            // causing thousands of reference errors, when `GetDiagnostics` is called below
            project2 = await project2.GetProjectWithReferencesAsync();

            var compilation = await project2.GetCompilationAsync();
            var diag = compilation.GetDiagnostics()
                .WhereAsArray(x => !x.IsWarningAsError && x.DefaultSeverity == DiagnosticSeverity.Error);

            using (var stream = new MemoryStream())
            {
                var emit = compilation.Emit(stream);
                stream.Flush();
                if (emit.Success)
                {
                    stream.Position = 0;
                }
            }

            if (diag.Length == 0)
            {
                var usedTypes = await compilation.FindUsedTypes(
                    syntaxTree => ConsoleHelper.WriteLine(
                        "Loading file: " + PathHelper.GetRelativePath(solution.FilePath, syntaxTree.FilePath),
                        ConsoleColor.Magenta,
                        1));

                var frameworkRequirements = new FrameworkRequirements
                    {
                        Project = project,
                        Recompile = false,
                        Types = usedTypes
                            .Select(RoslynExtensions.RoslynExtensions.GetTypeFullName)
                            .Where(t => t != "System.Runtime.InteropServices.GuidAttribute")
                            .Where(t => t != "System.Runtime.InteropServices.ComVisibleAttribute")
                            .ToArray(),
                    };

                // determining what frameworks support this set of types
                var supportedFrameworks = await hierarchyGraph.GetSupportGraphAsync(frameworkRequirements, false);

                var dependenciesSupportGraph = dependencies
                    .Select(x => x.SupportedFrameworks)
                    .Transpose()
                    .ToArray();

                var projectSupportGraph = ProjectSupportGraphs(supportedFrameworks, dependenciesSupportGraph);

                return new ProjectAnalysis(project2, projectSupportGraph);
            }

            return new ProjectAnalysis(project2, null);
        }

        private static ProjectSupportGraph[] ProjectSupportGraphs(
            IEnumerable<SupportGraph> supportedFrameworks,
            IEnumerable<ProjectSupportGraph[]> dependenciesSupportGraph)
        {
            var projectSupportGraph = supportedFrameworks.Zip(
                dependenciesSupportGraph,
                (s, ds) =>
                {
                    var dependenciesSupportGraph2 = ds
                        .Select(x => x.Subsets.Cast<ProjectSupportGraph>())
                        .Transpose()
                        .ToArray();

                    var children = ProjectSupportGraphs(
                        s.Subsets.Cast<SupportGraph>(),
                        dependenciesSupportGraph2);

                    return new ProjectSupportGraph(s, children, s.Result);
                })
                .ToArray();

            return projectSupportGraph;
        }
    }

    public class ProjectSupportGraph : SupportGraph
    {
        public ProjectSupportGraph(
            [NotNull] HierarchyGraph shadowGraph,
            IReadOnlyCollection<ProjectSupportGraph> children,
            FrameworkAnalysisResult result)
            : base(shadowGraph, children, result)
        {
        }
    }
}