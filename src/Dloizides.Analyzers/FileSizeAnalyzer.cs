using System.Collections.Immutable;
using System.IO;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileSizeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.FileSize);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var baseline = FileSizeBaseline.From(start.Options.AdditionalFiles, start.CancellationToken);
            start.RegisterSyntaxTreeAction(tree => Analyze(tree, baseline));
        });
    }

    private static void Analyze(SyntaxTreeAnalysisContext context, FileSizeBaseline baseline)
    {
        var path = context.Tree.FilePath;
        if (GeneratedPath.IsMatch(path))
            return;
        var lines = RawLineCount(context.Tree.GetText(context.CancellationToken));
        var allowed = baseline.AllowedLines(path);
        if (lines > allowed)
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.FileSize, Location.Create(context.Tree, new TextSpan(0, 0)), Path.GetFileName(path), lines, allowed));
    }

    private static int RawLineCount(SourceText text)
    {
        if (text.Length == 0)
            return 0;
        var count = text.Lines.Count;
        return text.Lines[count - 1].Span.Length == 0 ? count - 1 : count;
    }
}
