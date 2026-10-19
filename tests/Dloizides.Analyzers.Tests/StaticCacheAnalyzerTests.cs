namespace Dloizides.Analyzers.Tests;

public class StaticCacheAnalyzerTests
{
    private const string Dlz0009 = "DLZ0009";

    private const string Usings = "using System.Collections.Concurrent; using System.Collections.Generic;\n";

    [Theory]
    [InlineData("public byte[] Load(string name) => Fonts.GetOrAdd(name, key => new byte[0]);")]
    [InlineData("public void Store(string name, byte[] data) => Fonts[name] = data;")]
    [InlineData("public bool Store(string name) => Fonts.TryAdd(name, new byte[0]);")]
    [InlineData("public void Store(string name) => Fonts.AddOrUpdate(name, new byte[0], (key, old) => old);")]
    public async Task Analyze_WithStaticDictionaryWrittenAtRuntime_ReportsDlz0009(string member)
    {
        var source = Usings + $"public sealed class FontResolver {{ private static readonly ConcurrentDictionary<string, byte[]> Fonts = new(); {member} }}";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0009 }, ids);
    }

    [Theory]
    [InlineData("private static readonly ConcurrentDictionary<string, int> Codes = new(new Dictionary<string, int> { [\"a\"] = 1 }); public bool Find(string key, out int code) => Codes.TryGetValue(key, out code);")]
    [InlineData("private static readonly ConcurrentDictionary<string, int> Codes = new(); static Lookup() { Codes.TryAdd(\"a\", 1); Codes[\"b\"] = 2; } public int Get(string key) => Codes[key];")]
    [InlineData("private readonly ConcurrentDictionary<string, int> _codes = new(); public int Get(string key) => _codes.GetOrAdd(key, 1);")]
    [InlineData("private static readonly Dictionary<string, int> Codes = new() { [\"a\"] = 1 }; public int Get(string key) => Codes[key];")]
    public async Task Analyze_WithLookupTableOrInstanceCache_ReportsNothing(string members)
    {
        var source = Usings + $"public sealed class Lookup {{ {members} }}";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsNothing()
    {
        var source = Usings + "public sealed class Memo { private static readonly ConcurrentDictionary<string, int> Seen = new(); public int Get(string key) => Seen.GetOrAdd(key, 1); }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }
}
