using System.Globalization;
using System.Text.Json;
using FlowForge.App.Diagnostics;
using FluentAssertions;

namespace FlowForge.App.Tests.Performance;

public sealed class StressScenarioTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Create_SupportedNodeCount_ReturnsExpectedDeterministicScenario(int nodeCount)
    {
        var first = StressScenarioGenerator.Create(nodeCount, seed: 20260803);
        var second = StressScenarioGenerator.Create(nodeCount, seed: 20260803);

        first.Nodes.Should().HaveCount(nodeCount);
        first.Edges.Should().HaveCount(nodeCount - 1);
        first.Nodes.Select(node => node.Id).Should().Equal(second.Nodes.Select(node => node.Id));
        first.Nodes.Select(node => node.Position).Should().Equal(second.Nodes.Select(node => node.Position));
        first.Edges.Select(edge => edge.Id).Should().Equal(second.Edges.Select(edge => edge.Id));
        first.Edges.Select(edge => (edge.Source.Node.Id, edge.Target!.Node.Id))
            .Should()
            .Equal(second.Edges.Select(edge => (edge.Source.Node.Id, edge.Target!.Node.Id)));
    }

    [Fact]
    public void Create_DifferentSeed_ChangesDeterministicCoordinates()
    {
        var first = StressScenarioGenerator.Create(100, seed: 1);
        var second = StressScenarioGenerator.Create(100, seed: 2);

        first.Nodes[0].Position.Should().NotBe(second.Nodes[0].Position);
    }

    [Fact]
    public void Create_UnsupportedNodeCount_ThrowsArgumentOutOfRangeException()
    {
        var action = () => StressScenarioGenerator.Create(101, seed: 20260803);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PerfRunResult_ReportsAveragePeakAndJsonFields()
    {
        var result = new PerfRunResult(
            1000,
            20260803,
            DateTimeOffset.Parse("2026-08-03T00:00:00+08:00", CultureInfo.InvariantCulture),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            [
                new PerfSample(1000, 20260803, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), 60, 60, 120_000_000),
                new PerfSample(1000, 20260803, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), 58, 58, 121_000_000),
            ]);

        result.AverageFramesPerSecond.Should().Be(59);
        result.PeakWorkingSetBytes.Should().Be(121_000_000);

        using var json = JsonDocument.Parse(result.ToJson());
        json.RootElement.GetProperty("nodeCount").GetInt32().Should().Be(1000);
        json.RootElement.GetProperty("samples").GetArrayLength().Should().Be(2);
        json.RootElement.GetProperty("averageFramesPerSecond").GetDouble().Should().Be(59);
    }

    [Fact]
    public void PerfSamplingOptions_TryParsePerfArguments_ReturnsValidatedOptions()
    {
        var parsed = PerfSamplingOptions.TryParse(
            ["--perf", "--nodes", "250", "--seed=7", "--output", "artifacts/perf.json"],
            out var options);

        parsed.Should().BeTrue();
        options.NodeCount.Should().Be(250);
        options.Seed.Should().Be(7);
        options.OutputPath.Should().Be("artifacts/perf.json");
    }

    [Fact]
    public void PerfSamplingOptions_WithoutPerfFlag_ReturnsFalse()
    {
        var parsed = PerfSamplingOptions.TryParse([], out _);

        parsed.Should().BeFalse();
    }
}
