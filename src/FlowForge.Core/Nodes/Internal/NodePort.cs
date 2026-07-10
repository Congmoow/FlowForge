using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Nodes.Internal;

internal sealed class NodePort<T>(string id, string name) : IPort<T>
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public Type DataType => typeof(T);
}
