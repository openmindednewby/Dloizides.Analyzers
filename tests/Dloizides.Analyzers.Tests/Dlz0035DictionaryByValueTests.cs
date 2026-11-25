namespace Dloizides.Analyzers.Tests;

public class Dlz0035DictionaryByValueTests
{
    private const string Dlz0035 = "DLZ0035";
    private const string Dlz0019 = "DLZ0019";
    private const string Usings = "using System.Collections.Generic; using System.Linq; ";
    private const string SingleOrDefaultByValue = Usings + "namespace Sample { public sealed class Row { public string? Key(Dictionary<string, string> headers, int c) => headers.SingleOrDefault(h => h.Value == c.ToString()).Key; } }";

    [Theory]
    [InlineData(SingleOrDefaultByValue)]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public bool Has(IReadOnlyDictionary<string, int> map) => map.Any(pair => pair.Value > 1); } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public int Count(IDictionary<string, int> map) => map.Where(pair => pair.Value > 1).Count(); } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public int Hits(Dictionary<string, int> map, int[] values) { var hits = 0; foreach (var value in values) if (map.ContainsValue(value)) hits++; return hits; } } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public int Hits(Dictionary<string, int> map, int[] values) { var hits = 0; for (var i = 0; i < values.Length; i++) if (map.Values.Contains(values[i])) hits++; return hits; } } }")]
    public async Task Analyze_WithDictionarySearchedByValue_ReportsDlz0035(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new DictionaryByValueAnalyzer(), source);

        Assert.Equal(new[] { Dlz0035 }, ids);
    }

    [Theory]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public bool Has(Dictionary<string, int> map) => map.Any(pair => pair.Key.Length > 1); } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public bool Has(Dictionary<string, int> map, int value) => map.ContainsValue(value); } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public bool Has(List<KeyValuePair<string, int>> pairs) => pairs.Any(pair => pair.Value > 1); } }")]
    [InlineData(Usings + "namespace Sample { public sealed class Row { public string Key(Dictionary<int, string> byIndex, int c) => byIndex[c]; } }")]
    public async Task Analyze_WithKeyLookupOrSingleValueCheck_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new DictionaryByValueAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/dictionary-by-value-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new DictionaryByValueAnalyzer(), SingleOrDefaultByValue, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/dictionary-by-value-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new DictionaryByValueAnalyzer(), SingleOrDefaultByValue, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
