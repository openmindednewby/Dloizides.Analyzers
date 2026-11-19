namespace Dloizides.Analyzers.Tests;

public class Dlz0016LinqMethodSyntaxTests
{
    private const string Dlz0016 = "DLZ0016";
    private const string Prelude = "using System.Linq;\npublic sealed class Sample\n{\n    public object Run(int[] numbers, int[] others)\n    {\n        ";
    private const string Epilogue = "\n    }\n}\n";
    private const string Query = "return from n in numbers select n;";

    [Theory]
    [InlineData(Query)]
    [InlineData("return from n in numbers where n > 1 orderby n descending select n * 2;")]
    [InlineData("return from int n in numbers select n;")]
    [InlineData("return from n in numbers join o in others on n equals o select n + o;")]
    [InlineData("return (from n in numbers group n by n % 2 into g select g.Key).ToList();")]
    [InlineData("return from n in numbers let d = n * 2 select d;")]
    public async Task Analyze_WithQueryExpression_ReportsDlz0016(string body)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), Prelude + body + Epilogue);

        Assert.Equal(new[] { Dlz0016 }, ids);
    }

    [Fact]
    public async Task Analyze_WithQueryNestedInQuery_ReportsOnce()
    {
        var body = "return from n in numbers select (from o in others where o > n select o).Count();";

        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), Prelude + body + Epilogue);

        Assert.Equal(new[] { Dlz0016 }, ids);
    }

    [Theory]
    [InlineData("return numbers.Where(n => n > 1).Select(n => n * 2);")]
    [InlineData("return numbers.GroupBy(n => n % 2).Select(g => g.Key).ToList();")]
    [InlineData("return \"from n in numbers select n\";")]
    [InlineData("var from = numbers.Length; return from;")]
    public async Task Analyze_WithMethodSyntax_ReportsNothing(string body)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), Prelude + body + Epilogue);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData("/repo/src/Migrations/20260101_Init.cs")]
    [InlineData("/repo/src/obj/Debug/Sample.cs")]
    [InlineData("/repo/src/bin/Sample.cs")]
    [InlineData("/repo/src/Generated/Sample.cs")]
    [InlineData("/repo/src/Sample.g.cs")]
    [InlineData("/repo/src/Sample.Designer.cs")]
    [InlineData(@"C:\repo\src\Migrations\Init.cs")]
    public async Task Analyze_WithQueryInGeneratedPath_ReportsNothing(string path)
    {
        var ids = await HouseRuleRunner.IdsAsync(new LinqMethodSyntaxAnalyzer(), Prelude + Query + Epilogue, path);

        Assert.Empty(ids);
    }
}
