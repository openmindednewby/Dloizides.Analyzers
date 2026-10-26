using System;
using System.Collections.Immutable;
using System.Linq;

namespace Dloizides.Analyzers.Rules;

/// <summary>Finds the first path segment of a route template that is named after an action.</summary>
public static class ActionRouteSegment
{
    private const string ActionToken = "[action]";

    private static readonly ImmutableArray<string> ActionWords = ImmutableArray.Create(
        "get", "add", "list", "search", "save", "send", "edit", "set", "run",
        "create", "update", "delete", "remove", "fetch", "insert", "execute", "calculate");

    private static readonly ImmutableHashSet<string> NounEndings = ImmutableHashSet.Create(
        StringComparer.Ordinal, "s", "r", "rs", "er", "ers", "es", "ion", "ions", "or", "ors", "al", "ing", "ings");

    private static readonly ImmutableArray<string> ParticipleTails = ImmutableArray.Create("d", "ed");

    private static readonly ImmutableHashSet<string> ParticipleSuffixes = ImmutableHashSet.Create(
        StringComparer.Ordinal, "", "by", "on", "at", "date", "time", "from", "to");

    private static readonly ImmutableArray<string> NounPrefixes = ImmutableArray.Create(
        "setting", "setup", "settle", "address", "listing", "listen", "runtime", "runner", "editor", "edition");

    /// <summary>Returns the offending segment of <paramref name="route"/>, or null when every segment is a noun.</summary>
    public static string? Find(string route) => route.Split('/').FirstOrDefault(IsAction);

    private static bool IsAction(string segment)
    {
        if (string.Equals(segment, ActionToken, StringComparison.OrdinalIgnoreCase))
            return true;
        var token = LeadingToken(segment);
        if (token.Length == 0 || NounPrefixes.Any(noun => token.StartsWith(noun, StringComparison.Ordinal)))
            return false;
        return ActionWords.Any(word => token.StartsWith(word, StringComparison.Ordinal) && !IsNounForm(token.Substring(word.Length)));
    }

    private static bool IsNounForm(string rest) =>
        NounEndings.Contains(rest) ||
        ParticipleTails.Any(tail => rest.StartsWith(tail, StringComparison.Ordinal) && ParticipleSuffixes.Contains(rest.Substring(tail.Length)));

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
