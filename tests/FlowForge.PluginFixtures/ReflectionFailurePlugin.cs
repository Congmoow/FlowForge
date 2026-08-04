using FlowForge.Core.Abstractions;
using FlowForge.PluginFixture.Dependency;

namespace FlowForge.PluginFixtures;

public sealed class ExplicitFixturePlugin : INodePlugin
{
    public IReadOnlyList<NodeDefinition> Definitions { get; } =
    [
        NodeDefinition.FromJsonFactory(
            "fixture.plugin.valid",
            static (id, _) => new FixtureNode(id)),
    ];
}

public sealed class FixtureNode(Guid id) : INode
{
    public Guid Id { get; } = id;

    public string TypeId => "fixture.plugin.valid";

    public IReadOnlyList<IPort> Inputs { get; } = [];

    public IReadOnlyList<IPort> Outputs { get; } = [];

    public INodeConfig Config { get; set; } = new FixtureConfig();

    public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed record FixtureConfig : INodeConfig;

public sealed class ReflectionFailurePlugin : MissingBase, INodePlugin
{
    public MissingContract Contract { get; } = new();

    public IReadOnlyList<NodeDefinition> Definitions { get; } = [];
}

public sealed class FixtureMarker
{
    public static string Name => "fixture";
}
