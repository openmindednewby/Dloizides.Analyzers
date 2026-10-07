using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0005 for a type in a technical-type namespace such as Services or Helpers.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FeatureFolderAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> TechnicalSegments = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Services", "Helpers", "Utils", "Utilities", "Common");

    private static readonly string[] SharedAssemblySuffixes = { ".Shared", ".SharedKernel" };

    private const string TestAssemblySuffix = "Tests";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.FeatureFolders);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (IsExemptAssembly(start.Compilation.AssemblyName))
                return;
            start.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        });
    }

    private static bool IsExemptAssembly(string? assemblyName)
    {
        if (assemblyName is null)
            return false;
        var isTestAssembly = assemblyName.EndsWith(TestAssemblySuffix, StringComparison.Ordinal);
        return isTestAssembly || SharedAssemblySuffixes.Any(suffix => assemblyName.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.ContainingType is not null || type.IsImplicitlyDeclared || type.ContainingNamespace.IsGlobalNamespace)
            return;
        var segment = type.ContainingNamespace.ToDisplayString().Split('.').FirstOrDefault(TechnicalSegments.Contains);
        if (segment is null)
            return;
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.FeatureFolders, type.Locations[0], type.Name, segment));
    }
}
