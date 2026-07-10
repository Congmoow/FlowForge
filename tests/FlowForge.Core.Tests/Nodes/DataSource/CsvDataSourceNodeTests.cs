using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.DataSource;

namespace FlowForge.Core.Tests.Nodes.DataSource;

public sealed class CsvDataSourceNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new CsvDataSourceConfig("input.csv");

        var node = new CsvDataSourceNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.datasource.csv");
        node.Inputs.Should().BeEmpty();
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.RowsOutput);
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.RowsOutput.Id.Should().Be("rows");
        node.RowsOutput.DataType.Should().Be(typeof(IEnumerable<Dictionary<string, string>>));
        node.Config.Should().BeSameAs(config);
        config.HasHeader.Should().BeTrue();
        config.Delimiter.Should().Be(',');
        (config with { Delimiter = ';' }).Delimiter.Should().Be(';');
    }

    [Fact]
    public async Task ExecuteAsync_HeaderAndQuotedFields_WritesParsedRowsAsync()
    {
        await using var file = await TemporaryFile.CreateAsync(
            "name;note\r\nAlice;\"left;right\"\r\nBob;\"said \"\"hello\"\"\"");
        var node = new CsvDataSourceNode(new CsvDataSourceConfig(file.Path, true, ';'));
        var context = new TestExecutionContext();
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        var rows = context.GetOutput(node.RowsOutput)!.ToList();
        rows.Should().HaveCount(2);
        rows[0]["name"].Should().Be("Alice");
        rows[0]["note"].Should().Be("left;right");
        rows[1]["name"].Should().Be("Bob");
        rows[1]["note"].Should().Be("said \"hello\"");
        context.LastWritePort.Should().BeSameAs(node.RowsOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutHeader_GeneratesColumnNamesAsync()
    {
        await using var file = await TemporaryFile.CreateAsync("alpha,beta\r\ngamma,delta");
        var node = new CsvDataSourceNode(new CsvDataSourceConfig(file.Path, false, ','));
        var context = new TestExecutionContext();

        await node.ExecuteAsync(context, CancellationToken.None);

        var rows = context.GetOutput(node.RowsOutput)!.ToList();
        rows.Should().HaveCount(2);
        rows[0].Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Column1"] = "alpha",
            ["Column2"] = "beta",
        });
        rows[1].Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Column1"] = "gamma",
            ["Column2"] = "delta",
        });
    }

    [Fact]
    public async Task ExecuteAsync_MismatchedColumnCount_ThrowsFormatExceptionAsync()
    {
        await using var file = await TemporaryFile.CreateAsync("first,second\r\nvalue");
        var node = new CsvDataSourceNode(new CsvDataSourceConfig(file.Path));
        var context = new TestExecutionContext();

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<FormatException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_QuotedFieldSpansLines_ThrowsFormatExceptionAsync()
    {
        await using var file = await TemporaryFile.CreateAsync("first,second\r\nvalue,\"line one\r\nline two\"");
        var node = new CsvDataSourceNode(new CsvDataSourceConfig(file.Path));
        var context = new TestExecutionContext();

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<FormatException>();
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_StopsBeforeWritingAsync()
    {
        await using var file = await TemporaryFile.CreateAsync("first\r\nvalue");
        var node = new CsvDataSourceNode(new CsvDataSourceConfig(file.Path));
        var context = new TestExecutionContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await node.ExecuteAsync(context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        context.WriteCount.Should().Be(0);
    }
}
