using System.Reflection;

namespace AppLens.Backend.Tests;

public sealed class CoreBoundaryTests
{
    [Theory]
    [InlineData("TuneCollector")]
    [InlineData("TuneActionExecutor")]
    [InlineData("RulesEngine")]
    [InlineData("TunePlanBuilder")]
    [InlineData("ReadinessSummaryBuilder")]
    [InlineData("LocalAiProfileBuilder")]
    public void Core_assembly_does_not_ship_deferred_implementations(string type)
    {
        Assert.Null(typeof(AuditService).Assembly.GetType($"AppLens.Backend.{type}"));
    }

    [Fact]
    public void Core_does_not_reference_a_tune_assembly()
    {
        Assert.DoesNotContain(typeof(AuditService).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("Tune", StringComparison.OrdinalIgnoreCase) == true);
    }
}
