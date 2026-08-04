using System.Reflection;
using BenchmarkDotNet.Attributes;
using FluentAssertions;
using FlowForge.Benchmarks;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Performance;

public sealed class WorkflowSchedulerBenchmarkTests
{
    [Fact]
    public void NodeCount_DeclaresRequiredBenchmarkParameters()
    {
        var property = typeof(WorkflowSchedulerBenchmarks).GetProperty(
            nameof(WorkflowSchedulerBenchmarks.NodeCount));
        var paramsAttribute = property!
            .GetCustomAttributes(inherit: false)
            .OfType<ParamsAttribute>()
            .Single();

        paramsAttribute.Values.Cast<object>().Select(Convert.ToInt32).Should().Equal(10, 100, 1000);
    }

    [Fact]
    public void ExecuteAsync_IsAnAsyncBenchmark()
    {
        var method = typeof(WorkflowSchedulerBenchmarks).GetMethod(
            nameof(WorkflowSchedulerBenchmarks.ExecuteAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        method.GetCustomAttribute<BenchmarkAttribute>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task IterationSetup_CreatesIsolatedWorkflowAndRunsSchedulerAsync(int nodeCount)
    {
        var benchmark = new WorkflowSchedulerBenchmarks { NodeCount = nodeCount };

        benchmark.IterationSetup();
        var firstWorkflow = ReadWorkflow(benchmark);
        await benchmark.ExecuteAsync();

        benchmark.IterationSetup();
        var secondWorkflow = ReadWorkflow(benchmark);
        await benchmark.ExecuteAsync();

        secondWorkflow.Should().NotBeSameAs(firstWorkflow);
        secondWorkflow.Nodes.Should().HaveCount(nodeCount);
    }

    private static Workflow ReadWorkflow(WorkflowSchedulerBenchmarks benchmark)
    {
        var field = typeof(WorkflowSchedulerBenchmarks).GetField(
            "workflow",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (Workflow)field!.GetValue(benchmark)!;
    }
}
