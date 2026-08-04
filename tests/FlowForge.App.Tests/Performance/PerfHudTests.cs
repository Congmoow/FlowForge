using Avalonia.Controls;
using FlowForge.App.Controls;
using FlowForge.App.Diagnostics;
using FluentAssertions;

namespace FlowForge.App.Tests.Performance;

public sealed class PerfHudTests
{
    [Fact]
    public void FromSamplingOptions_PreservesFixedSamplingSchedule()
    {
        var sampling = new PerfSamplingOptions
        {
            NodeCount = 250,
            Seed = 7,
            OutputPath = "artifacts/perf.json",
        };

        var options = PerfRunOptions.From(sampling);

        options.NodeCount.Should().Be(250);
        options.Seed.Should().Be(7);
        options.Warmup.Should().Be(TimeSpan.FromSeconds(5));
        options.Duration.Should().Be(TimeSpan.FromSeconds(30));
        options.SampleInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.OutputPath.Should().Be("artifacts/perf.json");
        options.ToSamplingOptions().Should().BeEquivalentTo(sampling);
    }

    [Fact]
    public void Hud_StartAndApplySample_UpdatesOnlySamplingState()
    {
        var hud = new PerfHudViewModel();
        var changedProperties = new List<string?>();
        hud.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        var options = PerfRunOptions.From(new PerfSamplingOptions { NodeCount = 100 });

        hud.Start(options);
        changedProperties.Clear();
        hud.ApplySample(new PerfSample(
            100,
            options.Seed,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(1),
            58,
            58,
            123_456_789));

        hud.IsVisible.Should().BeTrue();
        hud.NodeCount.Should().Be(100);
        hud.FramesPerSecond.Should().Be(58);
        hud.WorkingSetBytes.Should().Be(123_456_789);
        hud.DisplayText.Should().Contain("FPS 58.0");
        hud.DisplayText.Should().Contain("节点 100");
        hud.DisplayText.Should().Contain("WorkingSet64 123456789 B");
        changedProperties.Distinct().Should().Contain(nameof(PerfHudViewModel.DisplayText));
        changedProperties.Count.Should().BeLessThan(10);
    }

    [Fact]
    public void PerfHudControl_IsPointerTransparentAndContainsTextPresenter()
    {
        var control = new PerfHudControl();

        control.IsHitTestVisible.Should().BeFalse();
        control.Child.Should().BeOfType<TextBlock>();
    }

    [Fact]
    public void Hud_WhenDisabled_RemainsHiddenAfterCompletion()
    {
        var hud = new PerfHudViewModel();
        var options = PerfRunOptions.From(new PerfSamplingOptions { NodeCount = 100 }) with
        {
            ShowHud = false,
        };

        hud.Start(options);
        hud.Complete(new PerfRunResult(
            100,
            options.Seed,
            DateTimeOffset.UtcNow,
            options.Warmup,
            options.Duration,
            []));

        hud.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Hud_Failure_ExposesErrorForUnattendedRun()
    {
        var hud = new PerfHudViewModel();
        hud.Start(PerfRunOptions.From(new PerfSamplingOptions { NodeCount = 100 }));
        var error = new InvalidOperationException("输出路径不可写");

        hud.Fail(error);

        hud.IsVisible.Should().BeTrue();
        hud.Phase.Should().Be("采样失败");
        hud.DisplayText.Should().Contain(error.Message);
    }
}
