using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers.Rules;

internal sealed class FileSizeBaseline
{
    public const string FileName = "file-size-baseline.txt";
    public const int Limit = 300;

    private readonly ViolationBaseline entries;

    private FileSizeBaseline(ViolationBaseline entries) => this.entries = entries;

    public static FileSizeBaseline From(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken) =>
        new(ViolationBaseline.From(additionalFiles, FileName, Descriptors.FileSize.Id, cancellationToken));

    public int AllowedLines(string path) => entries.Listed(path) is int lines && lines > Limit ? lines : Limit;

    public void ReportStale(SyntaxTreeAnalysisContext context, int lines) => entries.ReportStale(context, lines);
}
