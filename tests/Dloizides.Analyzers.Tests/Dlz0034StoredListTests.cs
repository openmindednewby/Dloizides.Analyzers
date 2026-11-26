namespace Dloizides.Analyzers.Tests;

public class Dlz0034StoredListTests
{
    private const string Dlz0034 = "DLZ0034";
    private const string StoredAndIndexed = "using System.Collections.Generic; namespace Sample { public sealed class Book { private readonly List<List<int>> _sheets; public Book(List<List<int>> sheets) { _sheets = sheets; } public int At(int sheet, int row) => _sheets[sheet][row] + _sheets.Count; } }";
    private const string PassedOnToReader = "using System.Collections.Generic; namespace Sample { public sealed class Book { private readonly List<int> _rows; private Book(List<int> rows) { this._rows = rows; } public Cursor Open() => new Cursor(_rows); } public sealed class Cursor { private readonly List<int> _rows; public Cursor(List<int> rows) { _rows = rows; } public int First => _rows[0]; } }";
    private const string PassedToEnumerable = "using System.Collections.Generic; using System.Linq; namespace Sample { public sealed class Job { public int Run(List<int> rows) => Total(rows); private static int Total(IEnumerable<int> rows) => rows.Sum(); } }";
    private const string InitializedAndCounted = "using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows = new List<int>(); public int Size => _rows.Count; } }";

    [Theory]
    [InlineData(StoredAndIndexed, 2)]
    [InlineData(PassedOnToReader, 4)]
    [InlineData(PassedToEnumerable, 1)]
    [InlineData(InitializedAndCounted, 1)]
    public async Task Analyze_WithListStoredOrPassedOnOnlyRead_ReportsDlz0034PerDeclaration(string source, int expected)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ReadOnlyParameterAnalyzer(), source);

        Assert.Equal(Enumerable.Repeat(Dlz0034, expected), ids);
    }

    [Theory]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows; public Job(List<int> rows) { _rows = rows; } public void Add() => _rows.Add(1); } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows; public Job(List<int> rows) { _rows = rows; } public List<int> Rows() => _rows; } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows; public Job(List<int> rows) { _rows = rows; } public void Reset() => Clear(_rows); private static void Clear(List<int> rows) => rows.Clear(); } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { public readonly List<int> Rows; public Job(List<int> rows) { Rows = rows; } } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private List<int> _rows; public Job(List<int> rows) { _rows = rows; } public int Size => _rows.Count; } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows; public Job(List<int> rows) { _rows = rows; } public void Bump() { _rows[0]++; } } }")]
    [InlineData("using System.Collections.Generic; namespace Sample { public sealed class Job { private readonly List<int> _rows; public Job(List<int> rows) { _rows = rows; } public void Order() => _rows.Sort(); } }")]
    public async Task Analyze_WithStoredListMutatedReturnedOrExposed_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ReadOnlyParameterAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithStoredAndIndexedList_ReportsAtTheFieldAndTheParameter()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new ReadOnlyParameterAnalyzer(), StoredAndIndexed, HouseRuleRunner.SamplePath);

        var reported = diagnostics
            .Select(diagnostic => StoredAndIndexed.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .OrderBy(text => text, StringComparer.Ordinal);
        Assert.Equal(new[] { "List<List<int>> sheets", "_sheets" }, reported);
    }
}
