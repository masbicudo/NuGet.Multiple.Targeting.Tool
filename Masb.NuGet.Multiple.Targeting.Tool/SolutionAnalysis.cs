using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Masb.NuGet.Multiple.Targeting.Tool.Graphs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Masb.NuGet.Multiple.Targeting.Tool
{
    public class SolutionAnalysis
    {
        [NotNull]
        public Solution Solution { get; private set; }

        [NotNull]
        public ProjectAnalysis[] ProjectAnalyses { get; private set; }

        private SolutionAnalysis([NotNull] Solution solution, [NotNull] ProjectAnalysis[] projectAnalyses)
        {
            if (solution == null)
                throw new ArgumentNullException("solution");

            if (projectAnalyses == null)
                throw new ArgumentNullException("projectAnalyses");

            this.Solution = solution;
            this.ProjectAnalyses = projectAnalyses;
        }

        public static async Task<SolutionAnalysis> AnalyseSolutionAsync(string slnPath, HierarchyGraph hierarchyGraph)
        {
            var workspace = MSBuildWorkspace.Create();
            try
            {
                if (!File.Exists(slnPath))
                    throw new FileNotFoundException("Solution file does not exist:\n - " + slnPath);

                var solution = await workspace.OpenSolutionAsync(slnPath);

                var sortedProjectIds = solution
                    .GetProjectDependencyGraph()
                    .GetTopologicallySortedProjects()
                    .ToArray();

                var projAnalyses = new Dictionary<ProjectId, ProjectAnalysis>();
                foreach (var projectId in sortedProjectIds)
                {
                    var project = solution.GetProject(projectId);
                    var dependencies = project.AllProjectReferences
                        .Select(x => projAnalyses[x.ProjectId])
                        .ToArray();

                    var prjAnalysis = await ProjectAnalysis.AnalyseProjectAsync(project, hierarchyGraph, dependencies);
                    solution = prjAnalysis.Project.Solution;

                    projAnalyses.Add(projectId, prjAnalysis);
                }

                // After analysing all projects, we need to filter supported frameworks
                // by project dependencies. One project cannot use a framework, if that
                // framework is not supported by projects depended upon.

                return new SolutionAnalysis(solution, sortedProjectIds.Select(id => projAnalyses[id]).ToArray());
            }
            finally
            {
                workspace.CloseSolution();
            }
        }
    }
}