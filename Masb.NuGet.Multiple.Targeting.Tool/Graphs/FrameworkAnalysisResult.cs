using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace Masb.NuGet.Multiple.Targeting.Tool.Graphs
{
    public sealed class FrameworkAnalysisResult
    {
        public bool IsOk { get; private set; }

        [CanBeNull]
        public List<string> UnsupportedTypes { get; private set; }

        public ImmutableArray<Diagnostic> CompilationDiagnostics { get; private set; }

        public FrameworkAnalysisResult([CanBeNull] List<string> unsupportedTypes)
        {
            this.UnsupportedTypes = unsupportedTypes;
            this.IsOk = false;
        }

        public FrameworkAnalysisResult(ImmutableArray<Diagnostic> compilationDiagnostics)
        {
            this.CompilationDiagnostics = compilationDiagnostics;
            this.IsOk = false;
        }

        public FrameworkAnalysisResult()
        {
            this.IsOk = true;
        }
    }
}