namespace Dloizides.Analyzers.Tests;

public class RecordContractAnalyzerTests
{
    private const string Dlz0012 = "DLZ0012";

    [Theory]
    [InlineData("public class OrderDto { public int Id { get; set; } }")]
    [InlineData("public sealed class PlaceOrderCommand { public int Id { get; init; } }")]
    [InlineData("public class OrderPlacedEvent { }")]
    public async Task Analyze_WithContractDeclaredAsClass_ReportsDlz0012(string source)
    {
        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0012 }, ids);
    }

    [Theory]
    [InlineData("public record OrderDto(int Id);")]
    [InlineData("public sealed record PlaceOrderCommand { public int Id { get; init; } }")]
    [InlineData("public record struct OrderPlacedEvent(int Id);")]
    [InlineData("public class OrderChangedEvent : System.EventArgs { }")]
    [InlineData("public class Event { }")]
    [InlineData("public class OrderService { }")]
    public async Task Analyze_WithRecordOrNonContractType_ReportsNothing(string source)
    {
        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsNothing()
    {
        var source = "public class OrderDto { public int Id { get; set; } }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }
}
