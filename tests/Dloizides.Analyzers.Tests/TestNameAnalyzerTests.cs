namespace Dloizides.Analyzers.Tests;

public class TestNameAnalyzerTests
{
    private const string Dlz0004 = "DLZ0004";

    private const string Attributes =
        "namespace Xunit { public class FactAttribute : System.Attribute { } public class TheoryAttribute : FactAttribute { } }\n" +
        "namespace NUnit.Framework { public class TestAttribute : System.Attribute { } }\n" +
        "namespace Microsoft.VisualStudio.TestTools.UnitTesting { public class TestMethodAttribute : System.Attribute { } }\n" +
        "public class SkippableFactAttribute : Xunit.FactAttribute { }\n";

    private static Task<IReadOnlyList<string>> IdsForAsync(string attribute, string name) =>
        AnalyzerRunner.IdsAsync($"{Attributes}public class Tests {{\n[{attribute}]\npublic void {name}() {{ }}\n}}");

    [Theory]
    [InlineData("Create_WithValidRequest_Returns201")]
    [InlineData("Create_WhenNameTaken_ReturnsConflict")]
    [InlineData("Resolve_WithoutClaim_ReturnsNull")]
    [InlineData("Price_GivenTwoItems_ReturnsSum")]
    [InlineData("GetAsync2_WithId42_ReturnsItem")]
    public async Task Analyze_WithMethodScenarioExpectedName_ReportsNothing(string name)
    {
        var ids = await IdsForAsync("Xunit.Fact", name);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData("ForgettingOneParkLeavesTheOther")]
    [InlineData("SummaryOnPublicMember_NoDiagnostic")]
    [InlineData("Create_ValidRequest_Returns201AndIsReadableById")]
    [InlineData("Resolve_ReturnsLiveSummary_WhenRedisNullAndClaimed")]
    [InlineData("Update_WithAlerts_ReturnsFailure_AndLogs")]
    [InlineData("T27_Create_WithValidRequest_Returns201")]
    [InlineData("create_WithValidRequest_Returns201")]
    [InlineData("Create_WithvalidRequest_Returns201")]
    [InlineData("Create_WithValidRequest_returns201")]
    [InlineData("Create_With_Returns201")]
    [InlineData("Create__WithValidRequest_Returns201")]
    public async Task Analyze_WithOtherNameShape_ReportsDlz0004(string name)
    {
        var ids = await IdsForAsync("Xunit.Fact", name);

        Assert.Equal(new[] { Dlz0004 }, ids);
    }

    [Theory]
    [InlineData("Xunit.Theory")]
    [InlineData("NUnit.Framework.Test")]
    [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod")]
    [InlineData("SkippableFact")]
    public async Task Analyze_WithBadNameOnEachTestAttribute_ReportsDlz0004(string attribute)
    {
        var ids = await IdsForAsync(attribute, "ReturnsSum");

        Assert.Equal(new[] { Dlz0004 }, ids);
    }

    [Fact]
    public async Task Analyze_WithBadNameOnNonTestMethod_ReportsNothing()
    {
        var ids = await AnalyzerRunner.IdsAsync($"{Attributes}public class Helpers {{\npublic void ReturnsSum() {{ }}\n}}");

        Assert.Empty(ids);
    }
}
