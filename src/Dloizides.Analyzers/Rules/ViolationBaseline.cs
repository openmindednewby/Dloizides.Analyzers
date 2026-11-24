using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Dloizides.Analyzers.Rules;

internal sealed class ViolationBaseline
{
    private const string CurrentFolderPrefix = "./";
    private static readonly char[] Separators = { ' ', '\t' };

    private readonly Dictionary<string, int> counts;
    private readonly string fileName;
    private readonly string ruleId;

    private ViolationBaseline(Dictionary<string, int> counts, string fileName, string ruleId)
    {
        this.counts = counts;
        this.fileName = fileName;
        this.ruleId = ruleId;
    }

    public static ViolationBaseline From(ImmutableArray<AdditionalText> additionalFiles, string fileName, string ruleId, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in additionalFiles.Where(f => string.Equals(Path.GetFileName(f.Path), fileName, StringComparison.OrdinalIgnoreCase)))
        {
            var root = GeneratedPath.Normalize(Path.GetDirectoryName(file.Path) ?? string.Empty).TrimEnd('/');
            var text = file.GetText(cancellationToken);
            if (text is null)
                continue;
            foreach (var line in text.Lines)
                AddEntry(entries, root, line.ToString());
        }

        return new ViolationBaseline(entries, fileName, ruleId);
    }

    public int? Listed(string path) => counts.TryGetValue(GeneratedPath.Normalize(path), out var count) ? count : null;

    public void Report(SyntaxTreeAnalysisContext context, IReadOnlyList<Diagnostic> violations) =>
        Report(context.Tree, context.ReportDiagnostic, violations);

    public void Report(SyntaxTree tree, Action<Diagnostic> report, IReadOnlyList<Diagnostic> violations)
    {
        var listed = Listed(tree.FilePath) ?? 0;
        if (violations.Count > listed)
        {
            foreach (var violation in violations)
                report(violation);
            if (listed > 0)
                ReportOverCount(tree, report, violations.Count, listed);
            return;
        }

        ReportStale(tree, report, violations.Count);
    }

    public void ReportStale(SyntaxTreeAnalysisContext context, int actual) =>
        ReportStale(context.Tree, context.ReportDiagnostic, actual);

    private void ReportStale(SyntaxTree tree, Action<Diagnostic> report, int actual)
    {
        var path = tree.FilePath;
        if (Listed(path) is int listed && actual < listed)
            report(Diagnostic.Create(
                Descriptors.StaleBaseline, Location.Create(tree, new TextSpan(0, 0)), Path.GetFileName(path), actual, ruleId, fileName, listed));
    }

    private void ReportOverCount(SyntaxTree tree, Action<Diagnostic> report, int actual, int listed) =>
        report(Diagnostic.Create(
            Descriptors.BaselineOverCount, Location.Create(tree, new TextSpan(0, 0)), Path.GetFileName(tree.FilePath), actual, ruleId, fileName, listed, actual - listed));

    private static void AddEntry(Dictionary<string, int> entries, string root, string line)
    {
        var trimmed = line.Trim();
        var split = trimmed.LastIndexOfAny(Separators);
        if (split <= 0 || !int.TryParse(trimmed.Substring(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            return;
        var relative = GeneratedPath.Normalize(trimmed.Substring(0, split).Trim());
        if (relative.StartsWith(CurrentFolderPrefix, StringComparison.Ordinal))
            relative = relative.Substring(CurrentFolderPrefix.Length);
        entries[root.Length == 0 ? relative : root + "/" + relative] = count;
    }
}
