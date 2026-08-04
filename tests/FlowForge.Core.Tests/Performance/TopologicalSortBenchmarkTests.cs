using System.Collections;
using FlowForge.Benchmarks;
using FluentAssertions;

namespace FlowForge.Core.Tests.Performance;

public sealed class TopologicalSortBenchmarkTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public void GlobalSetup_BuildsDagWithRequestedNodeCount(int nodeCount)
    {
        var benchmark = new TopologicalSortBenchmarks { NodeCount = nodeCount };

        benchmark.GlobalSetup();

        benchmark.Sort().Should().HaveCount(nodeCount);
    }

    [Fact]
    public void NodeCount_DeclaresRequiredBenchmarkParameters()
    {
        var property = typeof(TopologicalSortBenchmarks).GetProperty(nameof(TopologicalSortBenchmarks.NodeCount));
        var paramsAttribute = property!
            .GetCustomAttributes(inherit: false)
            .Single(attribute => attribute.GetType().Name == "ParamsAttribute");
        var values = (IEnumerable)paramsAttribute.GetType().GetProperty("Values")!.GetValue(paramsAttribute)!;

        values.Cast<object>().Select(Convert.ToInt32).Should().Equal(100, 1000);
    }

    [Fact]
    public void BenchmarkType_IsNotSealed()
    {
        typeof(TopologicalSortBenchmarks).IsSealed.Should().BeFalse();
    }
}
