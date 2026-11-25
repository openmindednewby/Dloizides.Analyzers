using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NarrowOptionsAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "narrow-options-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.NarrowOptions, Descriptors.NarrowSettingsParameter, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.NarrowOptions.Id, start.CancellationToken);
            start.RegisterSemanticModelAction(model => Analyze(model, baseline));
        });
    }

    private static void Analyze(SemanticModelAnalysisContext context, ViolationBaseline baseline)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (GeneratedPath.IsMatch(tree.FilePath))
            return;
        var violations = tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(type => !type.Modifiers.Any(SyntaxKind.PartialKeyword))
            .SelectMany(type => type.Members
                .OfType<BaseMethodDeclarationSyntax>()
                .Where(member => IsChecked(context.SemanticModel, member, context.CancellationToken))
                .SelectMany(member => member.ParameterList.Parameters)
                .Select(parameter => OptionsSectionUse.ToDiagnostic(context.SemanticModel, type, parameter, context.CancellationToken)
                    ?? SettingsParameterUse.ToDiagnostic(context.SemanticModel, type, parameter, context.CancellationToken)))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static bool IsChecked(SemanticModel model, BaseMethodDeclarationSyntax member, CancellationToken cancellationToken) =>
        member switch
        {
            ConstructorDeclarationSyntax => true,
            MethodDeclarationSyntax method => (method.Body is not null || method.ExpressionBody is not null)
                && model.GetDeclaredSymbol(method, cancellationToken) is IMethodSymbol symbol
                && !ContractMethod.Is(symbol),
            _ => false,
        };
}
