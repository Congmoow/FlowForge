using System.Reflection;
using FluentAssertions;
using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Tests.Abstractions;

public sealed class NodeContractTests
{
    [Fact]
    public void PortOfT_DataType_ReturnsGenericType()
    {
        IPort port = new TestPort<string>("input", "输入");

        port.Id.Should().Be("input");
        port.Name.Should().Be("输入");
        port.DataType.Should().Be(typeof(string));
    }

    [Fact]
    public void Node_Ports_ExposeReadOnlyCollections()
    {
        var input = new TestPort<string>("input", "输入");
        var output = new TestPort<int>("output", "输出");
        INode node = new TestNode(input, output);

        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(input);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(output);
        node.Inputs.Should().BeAssignableTo<IReadOnlyList<IPort>>();
        node.Outputs.Should().BeAssignableTo<IReadOnlyList<IPort>>();
    }

    [Fact]
    public void ExecutionContext_Methods_UseTypedPortsAndCancellationTokens()
    {
        var methods = typeof(IExecutionContext).GetMethods(BindingFlags.Instance | BindingFlags.Public);

        methods.Should().ContainSingle(method =>
            method.Name == nameof(IExecutionContext.ReadAsync)
            && method.IsGenericMethodDefinition
            && HasParameters(method, typeof(IPort<>), typeof(CancellationToken)));
        methods.Should().ContainSingle(method =>
            method.Name == nameof(IExecutionContext.ReadAllAsync)
            && method.IsGenericMethodDefinition
            && HasParameters(method, typeof(IPort<>), typeof(CancellationToken)));
        methods.Should().ContainSingle(method =>
            method.Name == nameof(IExecutionContext.WriteAsync)
            && method.IsGenericMethodDefinition
            && HasParameters(method, typeof(IPort<>), null, typeof(CancellationToken)));
    }

    private static bool HasParameters(MethodInfo method, params Type?[] expectedTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != expectedTypes.Length)
        {
            return false;
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            var expectedType = expectedTypes[index];
            if (expectedType is null)
            {
                if (!parameters[index].ParameterType.IsGenericParameter)
                {
                    return false;
                }

                continue;
            }

            var parameterType = parameters[index].ParameterType;
            if (expectedType.IsGenericTypeDefinition)
            {
                if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != expectedType)
                {
                    return false;
                }

                continue;
            }

            if (parameterType != expectedType)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record TestConfig : INodeConfig;

    private sealed class TestPort<T>(string id, string name) : IPort<T>
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public Type DataType => typeof(T);
    }

    private sealed class TestNode(IPort input, IPort output) : INode
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string TypeId => "test.node";

        public IReadOnlyList<IPort> Inputs { get; } = [input];

        public IReadOnlyList<IPort> Outputs { get; } = [output];

        public INodeConfig Config { get; set; } = new TestConfig();

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
