using System.Collections;
using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Nodes.Internal;

internal sealed class NodePortList : IReadOnlyList<IPort>
{
    private readonly IPort[] _ports;

    private NodePortList(IPort[] ports)
    {
        _ports = ports;
    }

    public static NodePortList Empty { get; } = new([]);

    public IPort this[int index] => _ports[index];

    public int Count => _ports.Length;

    public static NodePortList Create(params IPort[] ports)
    {
        return ports.Length == 0
            ? Empty
            : new NodePortList((IPort[])ports.Clone());
    }

    public IEnumerator<IPort> GetEnumerator()
    {
        return ((IEnumerable<IPort>)_ports).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
