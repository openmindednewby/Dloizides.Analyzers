using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0011 once per compilation when HouseArchitectureStyle is missing or not an approved style.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchitectureStyleAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.ArchitectureStyle);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var style = ArchitectureStyle.Read(context.Options);
        if (ArchitectureStyle.IsKnown(style))
            return;
        var shown = style.Length == 0 ? "(not set)" : style;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.ArchitectureStyle, Location.None, shown));
    }
}
