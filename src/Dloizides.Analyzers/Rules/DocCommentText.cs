using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dloizides.Analyzers.Rules;

internal static class DocCommentText
{
    public const int MaxLines = 3;

    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "param", "returns", "inheritdoc", "see", "c", "paramref", "typeparamref",
    };

    private static readonly Regex ElementName = new(@"<\s*/?\s*([A-Za-z][\w\-]*)", RegexOptions.Compiled);
    private static readonly Regex SummaryDelimiter = new(@"^<\s*/?\s*summary\s*>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InheritDoc = new(@"^<\s*inheritdoc\b[^>]*/>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? FindViolation(string docText)
    {
        var lines = ContentLines(docText).ToList();
        var banned = lines
            .SelectMany(line => ElementName.Matches(line).Cast<Match>())
            .Select(match => match.Groups[1].Value)
            .FirstOrDefault(name => !AllowedElements.Contains(name));
        if (banned is not null)
            return $"Doc comment uses <{banned}>; only <summary>, <param> and <returns> are allowed";
        var counted = lines.Count(line => !SummaryDelimiter.IsMatch(line) && !InheritDoc.IsMatch(line));
        return counted > MaxLines
            ? $"Doc comment has {counted} lines; the cap is {MaxLines}"
            : null;
    }

    private static IEnumerable<string> ContentLines(string docText) =>
        docText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(StripDelimiters)
            .Where(line => line.Length > 0);

    private static string StripDelimiters(string raw)
    {
        var line = raw.Trim();
        if (line.StartsWith("///", StringComparison.Ordinal))
            line = line.Substring(3);
        else if (line.StartsWith("/**", StringComparison.Ordinal))
            line = line.Substring(3);
        if (line.EndsWith("*/", StringComparison.Ordinal))
            line = line.Substring(0, line.Length - 2);
        line = line.Trim();
        if (line.StartsWith("*", StringComparison.Ordinal))
            line = line.Substring(1);
        return line.Trim();
    }
}
