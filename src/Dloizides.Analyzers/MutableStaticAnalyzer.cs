using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0025 for a writable static field or a static property with a setter outside test assemblies.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableStaticAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.MutableStatic);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            start.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        });
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;
        if (!field.IsStatic || field.IsConst || field.IsReadOnly || field.IsImplicitlyDeclared)
            return;
        Report(context, "field", field);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (!property.IsStatic || property.SetMethod is null || property.IsImplicitlyDeclared)
            return;
        Report(context, "property", property);
    }

    private static void Report(SymbolAnalysisContext context, string kind, ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.MutableStatic, location, kind, symbol.Name));
        }
    }
}
