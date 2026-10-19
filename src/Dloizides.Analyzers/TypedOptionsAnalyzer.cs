using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0007 for IConfiguration taken by a constructor or held in a field.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypedOptionsAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> HostTypeNames = ImmutableHashSet.Create(StringComparer.Ordinal, "Startup", "Program");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.TypedOptions);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var configuration = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            if (configuration is null || AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterSymbolAction(symbol => AnalyzeConstructor(symbol, configuration), SymbolKind.Method);
            start.RegisterSymbolAction(symbol => AnalyzeField(symbol, configuration), SymbolKind.Field);
        });
    }

    private static void AnalyzeConstructor(SymbolAnalysisContext context, INamedTypeSymbol configuration)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Constructor || IsHostType(method.ContainingType))
            return;
        foreach (var parameter in method.Parameters.Where(p => IsConfiguration(p.Type, configuration)))
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.TypedOptions, parameter.Locations[0], method.ContainingType.Name));
    }

    private static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol configuration)
    {
        var field = (IFieldSymbol)context.Symbol;
        var type = field.ContainingType;
        if (field.IsImplicitlyDeclared || !IsConfiguration(field.Type, configuration) || IsHostType(type))
            return;
        var constructorTakesIt = type.Constructors.Any(c => c.Parameters.Any(p => IsConfiguration(p.Type, configuration)));
        if (constructorTakesIt)
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.TypedOptions, field.Locations[0], type.Name));
    }

    private static bool IsHostType(INamedTypeSymbol type) => type.IsStatic || HostTypeNames.Contains(type.Name);

    private static bool IsConfiguration(ITypeSymbol type, INamedTypeSymbol configuration) =>
        SymbolEqualityComparer.Default.Equals(type, configuration)
        || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, configuration));
}
