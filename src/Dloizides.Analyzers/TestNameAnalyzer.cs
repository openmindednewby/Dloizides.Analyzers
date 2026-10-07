using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0004 for an xUnit, NUnit or MSTest test method not named Method_Scenario_Expected.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> TestAttributeNames = ImmutableHashSet.Create(
        "FactAttribute", "TheoryAttribute", "TestAttribute", "TestMethodAttribute");

    private static readonly Regex MethodScenarioExpected = new(
        "^[A-Z][A-Za-z0-9]*_(With|When|Without|Given)[A-Z][A-Za-z0-9]*_[A-Z][A-Za-z0-9]*$",
        RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.TestNameShape);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!method.GetAttributes().Any(IsTestAttribute) || MethodScenarioExpected.IsMatch(method.Name))
            return;
        context.ReportDiagnostic(Diagnostic.Create(Descriptors.TestNameShape, method.Locations[0], method.Name));
    }

    private static bool IsTestAttribute(AttributeData attribute)
    {
        for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            if (TestAttributeNames.Contains(type.Name))
                return true;
        return false;
    }
}
