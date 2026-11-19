using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal sealed class FileSizeBaseline
{
    public const string FileName = "file-size-baseline.txt";
    public const int Limit = 300;

    private const string CurrentFolderPrefix = "./";
    private static readonly char[] Separators = { ' ', '\t' };

    private readonly Dictionary<string, int> allowedLines;

    private FileSizeBaseline(Dictionary<string, int> allowedLines) => this.allowedLines = allowedLines;

    public static FileSizeBaseline From(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in additionalFiles.Where(f => string.Equals(Path.GetFileName(f.Path), FileName, StringComparison.OrdinalIgnoreCase)))
        {
            var root = GeneratedPath.Normalize(Path.GetDirectoryName(file.Path) ?? string.Empty).TrimEnd('/');
            var text = file.GetText(cancellationToken);
            if (text is null)
                continue;
            foreach (var line in text.Lines)
                AddEntry(entries, root, line.ToString());
        }

        return new FileSizeBaseline(entries);
    }

    public int AllowedLines(string path) =>
        allowedLines.TryGetValue(GeneratedPath.Normalize(path), out var lines) && lines > Limit ? lines : Limit;

    private static void AddEntry(Dictionary<string, int> entries, string root, string line)
    {
        var trimmed = line.Trim();
        var split = trimmed.LastIndexOfAny(Separators);
        if (split <= 0 || !int.TryParse(trimmed.Substring(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var lines))
            return;
        var relative = GeneratedPath.Normalize(trimmed.Substring(0, split).Trim());
        if (relative.StartsWith(CurrentFolderPrefix, StringComparison.Ordinal))
            relative = relative.Substring(CurrentFolderPrefix.Length);
        entries[root.Length == 0 ? relative : root + "/" + relative] = lines;
    }
}
