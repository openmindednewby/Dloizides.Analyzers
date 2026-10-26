using System;
using System.Collections.Immutable;
using System.Linq;

namespace Dloizides.Analyzers.Rules;

/// <summary>Finds the first path segment of a route template that is named after an action.</summary>
public static class ActionRouteSegment
{
    private static readonly ImmutableHashSet<string> WholeTokenWords = ImmutableHashSet.Create(
        StringComparer.Ordinal, "get", "add", "list", "search", "save", "send", "edit", "set", "run");

    private static readonly ImmutableArray<string> PrefixWords = ImmutableArray.Create(
        "create", "update", "delete", "remove", "fetch", "insert", "execute", "calculate");

    private static readonly ImmutableHashSet<string> NounEndings = ImmutableHashSet.Create(
        StringComparer.Ordinal, "s", "d", "r", "rs", "es", "ed", "ion", "ions", "or", "ors", "al", "ing", "ings");

    /// <summary>Returns the offending segment of <paramref name="route"/>, or null when every segment is a noun.</summary>
    public static string? Find(string route) => route.Split('/').FirstOrDefault(IsAction);

    private static bool IsAction(string segment)
    {
        var token = LeadingToken(segment);
        if (token.Length == 0)
            return false;
        if (WholeTokenWords.Contains(token))
            return true;
        return PrefixWords.Any(word => token.StartsWith(word, StringComparison.Ordinal) && !NounEndings.Contains(token.Substring(word.Length)));
    }

    private static string LeadingToken(string segment)
    {
        if (segment.Length == 0 || !char.IsLetter(segment[0]))
            return string.Empty;
        var end = 1;
        while (end < segment.Length && (char.IsLower(segment[end]) || char.IsDigit(segment[end])))
            end++;
        return segment.Substring(0, end).ToLowerInvariant();
    }
}
