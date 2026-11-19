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

    public void Report(SyntaxTreeAnalysisContext context, IReadOnlyList<Diagnostic> violations)
    {
        if (violations.Count > (Listed(context.Tree.FilePath) ?? 0))
        {
            foreach (var violation in violations)
                context.ReportDiagnostic(violation);
            return;
        }

        ReportStale(context, violations.Count);
    }

    public void ReportStale(SyntaxTreeAnalysisContext context, int actual)
    {
        var path = context.Tree.FilePath;
        if (Listed(path) is int listed && actual < listed)
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.StaleBaseline, Location.Create(context.Tree, new TextSpan(0, 0)), Path.GetFileName(path), actual, ruleId, fileName, listed));
    }

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
