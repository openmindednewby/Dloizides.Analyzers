namespace Dloizides.Analyzers.Tests;

public class Dlz0031CatchScopeTests
{
    private const string Dlz0031 = "DLZ0031";
    private const string Types = "using System; using System.Collections.Generic; using System.Threading.Tasks; namespace Microsoft.EntityFrameworkCore { public class DbContext { public Task<int> SaveChangesAsync() => Task.FromResult(0); } } namespace Sample { public interface IOrderRepository { Task SaveAsync(int id); } public sealed class Clock { public Task TickAsync() => Task.CompletedTask; } public sealed class ShopDb : Microsoft.EntityFrameworkCore.DbContext { public Clock Clock { get; } = new(); } public sealed class Unit { public Task<int> SaveChangesAsync() => Task.FromResult(0); } ";
    private const string LoopOverRepository = Types + "public sealed class Job { public async Task RunAsync(IOrderRepository repository, List<int> ids) { foreach (var id in ids) { try { await repository.SaveAsync(id); } catch (Exception) { Console.WriteLine(id); } } } } }";

    [Theory]
    [InlineData(LoopOverRepository)]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(IOrderRepository repository, List<int> ids) { foreach (var id in ids) { try { await repository.SaveAsync(id).ConfigureAwait(false); } catch (Exception ex) when (ex is not OperationCanceledException) { } } } } }")]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(IOrderRepository repository) { var i = 0; while (i < 3) { try { await repository.SaveAsync(i); } catch { } i++; } } } }")]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(Unit unit) { for (var i = 0; i < 3; i++) { try { await unit.SaveChangesAsync(); } catch (Exception) { } } } } }")]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(ShopDb db) { for (var i = 0; i < 3; i++) { try { await db.Clock.TickAsync(); } catch (Exception) { } } } } }")]
    public async Task Analyze_WithSkippingCatchAroundSharedDependencyInLoop_ReportsDlz0031(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new CatchScopeAnalyzer(), source);

        Assert.Equal(new[] { Dlz0031 }, ids);
    }

    [Theory]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(IOrderRepository repository, List<int> ids) { foreach (var id in ids) { try { await repository.SaveAsync(id); } catch (Exception) { Console.WriteLine(id); throw; } } } } }")]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(IOrderRepository repository) { try { await repository.SaveAsync(1); } catch (Exception) { } } } }")]
    [InlineData(Types + "public sealed class Job { public async Task RunAsync(Clock clock, List<int> ids) { foreach (var id in ids) { try { await clock.TickAsync(); } catch (Exception) { } } } } }")]
    [InlineData(Types + "public sealed class Job { public void Run(IOrderRepository repository, List<int> ids) { foreach (var id in ids) { Func<Task> save = async () => { try { await repository.SaveAsync(id); } catch (Exception) { } }; } } } }")]
    public async Task Analyze_WithRethrowNoLoopOrNoSharedDependency_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new CatchScopeAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithRepositoryInTry_NamesTheRepositoryType()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new CatchScopeAnalyzer(), LoopOverRepository, HouseRuleRunner.SamplePath);

        Assert.Contains("'IOrderRepository'", Assert.Single(diagnostics).GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/catch-scope-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new CatchScopeAnalyzer(), LoopOverRepository, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }
}
