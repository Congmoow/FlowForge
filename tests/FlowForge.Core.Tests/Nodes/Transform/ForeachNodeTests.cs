using System.Collections;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Transform;

namespace FlowForge.Core.Tests.Nodes.Transform;

public sealed class ForeachNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new ForeachNodeConfig();

        var node = new ForeachNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.transform.foreach");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.ItemsInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.ItemOutput);
        node.Inputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.ItemsInput.Id.Should().Be("items");
        node.ItemsInput.DataType.Should().Be(typeof(IEnumerable));
        node.ItemOutput.Id.Should().Be("item");
        node.ItemOutput.DataType.Should().Be(typeof(object));
        node.Config.Should().BeSameAs(config);
        (config with { }).Should().Be(config);
    }

    [Fact]
    public async Task ExecuteAsync_Items_WritesEveryItemAsStreamAsync()
    {
        var node = new ForeachNode();
        var context = new TestExecutionContext();
        IEnumerable items = new object?[] { "first", 2, "third" };
        context.SetInput(node.ItemsInput, items);
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        context.WriteCount.Should().Be(3);
        context.GetOutput(node.ItemOutput).Should().Be("third");
        context.LastReadPort.Should().BeSameAs(node.ItemsInput);
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWritePort.Should().BeSameAs(node.ItemOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NullItems_WritesEmptyStreamAsync()
    {
        var node = new ForeachNode();
        var context = new TestExecutionContext();
        context.SetInput<IEnumerable>(node.ItemsInput, null);

        await node.ExecuteAsync(context, CancellationToken.None);

        context.WriteCount.Should().Be(0);
        context.LastWritePort.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeSecondItem_StopsBeforeWritingItAsync()
    {
        var node = new ForeachNode();
        var context = new TestExecutionContext();
        using var cancellation = new CancellationTokenSource();
        context.SetInput<IEnumerable>(
            node.ItemsInput,
            new CancelOnSecondMoveEnumerable(cancellation, ["first", "second"]));

        var action = async () => await node.ExecuteAsync(context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        context.WriteCount.Should().Be(1);
        context.GetOutput(node.ItemOutput).Should().Be("first");
    }

    private sealed class CancelOnSecondMoveEnumerable(
        CancellationTokenSource cancellation,
        object?[] values) : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            return new Enumerator(cancellation, values);
        }

        private sealed class Enumerator(
            CancellationTokenSource cancellation,
            object?[] values) : IEnumerator
        {
            private int _index = -1;

            public object? Current => values[_index];

            object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                _index++;
                if (_index == 1)
                {
                    cancellation.Cancel();
                }

                return _index < values.Length;
            }

            public void Reset()
            {
                _index = -1;
            }

            public void Dispose()
            {
                _index = -1;
            }
        }
    }
}
