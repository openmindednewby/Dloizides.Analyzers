namespace Dloizides.Analyzers.Tests;

public class Dlz0028ExceptionFlowTests
{
    private const string Dlz0028 = "DLZ0028";
    private const string Dlz0019 = "DLZ0019";
    private const string Open = "namespace Sample { public sealed class Reader { ";
    private const string Close = " } }";
    private const string ReturnZero = "public int Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { return 0; } }";

    [Theory]
    [InlineData(ReturnZero)]
    [InlineData("public int? Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { return null; } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { return default; } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.OverflowException) { return default(int); } }")]
    [InlineData("public int Read(object value) { try { return (int)value; } catch (System.InvalidCastException) { return -1; } }")]
    [InlineData("public int Read(string text) { var result = 1; try { result = int.Parse(text); } catch (System.FormatException ex) { result = 0; } return result; }")]
    [InlineData("public void Read(string text) { try { _ = int.Parse(text); } catch (System.FormatException) { } }")]
    [InlineData("public void Read(string[] texts) { foreach (var text in texts) { try { _ = int.Parse(text); } catch (System.FormatException) { continue; } } }")]
    public async Task Analyze_WithCatchOfParseExceptionReturningConstant_ReportsDlz0028(string member)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ExceptionFlowAnalyzer(), Open + member + Close);

        Assert.Equal(new[] { Dlz0028 }, ids);
    }

    [Theory]
    [InlineData("public int Read(string text) => int.TryParse(text, out var value) ? value : 0;")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { throw new System.InvalidOperationException(text); } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { return text.Length; } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.Exception) { return 0; } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.ArgumentException) { return 0; } }")]
    [InlineData("public int Read(string text) { try { return int.Parse(text); } catch (System.FormatException) { System.Console.WriteLine(text); return 0; } }")]
    public async Task Analyze_WithTryParseOrHandledException_ReportsNothing(string member)
    {
        var ids = await HouseRuleRunner.IdsAsync(new ExceptionFlowAnalyzer(), Open + member + Close);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithGeneratedPath_ReportsNothing()
    {
        var ids = await HouseRuleRunner.IdsAsync(new ExceptionFlowAnalyzer(), Open + ReturnZero + Close, "/repo/src/obj/Debug/Sample.cs");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/exception-flow-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new ExceptionFlowAnalyzer(), Open + ReturnZero + Close, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/exception-flow-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new ExceptionFlowAnalyzer(), Open + ReturnZero + Close, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
