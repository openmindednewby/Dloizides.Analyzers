using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "readonly-param-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.ReadOnlyParameter, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.ReadOnlyParameter.Id, start.CancellationToken);
            start.RegisterSemanticModelAction(model => Analyze(model, baseline));
        });
    }

    private static void Analyze(SemanticModelAnalysisContext context, ViolationBaseline baseline)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (GeneratedPath.IsMatch(tree.FilePath))
            return;
        var uses = new ReadOnlyListUses(context.SemanticModel, context.CancellationToken);
        var nodes = tree.GetRoot(context.CancellationToken).DescendantNodes().ToList();
        var parameters = nodes
            .OfType<BaseMethodDeclarationSyntax>()
            .Where(method => method is MethodDeclarationSyntax or ConstructorDeclarationSyntax)
            .SelectMany(method => method.ParameterList.Parameters)
            .Cast<SyntaxNode>();
        var fields = nodes
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(field => field.Declaration.Variables);
        var violations = parameters
            .Concat(fields)
            .Select(node => ToDiagnostic(context.SemanticModel, uses, node, context.CancellationToken))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static Diagnostic? ToDiagnostic(SemanticModel model, ReadOnlyListUses uses, SyntaxNode node, CancellationToken cancellationToken) =>
        model.GetDeclaredSymbol(node, cancellationToken) is { } symbol
        && TypeOf(symbol) is INamedTypeSymbol list
        && ReadOnlyListUses.IsList(list)
        && uses.IsOnlyRead(symbol)
            ? Diagnostic.Create(Descriptors.ReadOnlyParameter, node.GetLocation(), symbol.Name, list.TypeArguments[0].ToDisplayString())
            : null;

    private static ITypeSymbol? TypeOf(ISymbol symbol) => symbol switch
    {
        IParameterSymbol parameter => parameter.Type,
        IFieldSymbol field => field.Type,
        _ => null,
    };
}
