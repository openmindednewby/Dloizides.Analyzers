using System;
using System.Text.RegularExpressions;

namespace Dloizides.Analyzers.Rules;

internal static class GeneratedPath
{
    private static readonly Regex Pattern = new(
        @"(^|/)(obj|bin)/|\.g\.cs$|\.Designer\.cs$|ModelSnapshot\.cs$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    public static bool IsMatch(string? path) => path is not null && Pattern.IsMatch(Normalize(path));

    public static string Normalize(string path) => path.Replace('\\', '/');
}
