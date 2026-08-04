using System.Globalization;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Transform;
using System.Text.RegularExpressions;

namespace FlowForge.Core.Tests.Nodes.Transform;

public sealed class RegexExtractNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new RegexExtractNodeConfig(@"(?<word>\w+)", 1);

        var node = new RegexExtractNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.transform.regex-extract");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.TextInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.MatchesOutput);
        node.Inputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.TextInput.Id.Should().Be("text");
        node.TextInput.DataType.Should().Be(typeof(string));
        node.MatchesOutput.Id.Should().Be("matches");
        node.MatchesOutput.DataType.Should().Be(typeof(string[]));
        node.Config.Should().BeSameAs(config);
        (config with { Group = 0 }).Group.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPattern_WritesSelectedGroupValuesAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig(@"(?<word>[A-Za-z]+)", 1));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "alpha 42 beta");
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        context.GetOutput(node.MatchesOutput).Should().Equal("alpha", "beta");
        context.LastReadPort.Should().BeSameAs(node.TextInput);
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWritePort.Should().BeSameAs(node.MatchesOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_WritesEmptyArrayAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("cat"));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "dog");

        await node.ExecuteAsync(context, CancellationToken.None);

        context.GetOutput(node.MatchesOutput).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_UsesCultureInvariantMatchingAsync()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var node = new RegexExtractNode(new RegexExtractNodeConfig("(?i)i"));
            var context = new TestExecutionContext();
            context.SetInput(node.TextInput, "İ");

            await node.ExecuteAsync(context, CancellationToken.None);

            context.GetOutput(node.MatchesOutput).Should().BeEmpty();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPattern_ThrowsArgumentExceptionAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("["));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "text");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidGroup_ThrowsArgumentExceptionAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("(word)", 2));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "word");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CatastrophicPattern_ThrowsRegexTimeoutAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("^(a+)+$"));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, new string('a', 100_000) + "!");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<RegexMatchTimeoutException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_StopsBeforeMatchingAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("word"));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "word");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await node.ExecuteAsync(context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringManyMatches_StopsBeforeWritingAsync()
    {
        var node = new RegexExtractNode(new RegexExtractNodeConfig("a"));
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, string.Concat(Enumerable.Repeat("a ", 2_000_000)));
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var actionTask = node.ExecuteAsync(context, cancellation.Token).AsTask();
        var completedTask = await Task.WhenAny(actionTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
        var action = async () => await actionTask;

        completedTask.Should().BeSameAs(actionTask);
        await action.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
        context.WriteCount.Should().Be(0);
    }
}
