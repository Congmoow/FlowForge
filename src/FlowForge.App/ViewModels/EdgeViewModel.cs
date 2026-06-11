using Avalonia;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 节点连线 ViewModel，描述正式连线或拖拽中的草稿连线。
/// </summary>
public sealed class EdgeViewModel : ReactiveObject
{
    private EdgeViewModel(Guid id, PortViewModel source, PortViewModel? target, Point draftEndPoint)
    {
        ArgumentNullException.ThrowIfNull(source);

        Id = id;
        Source = source;
        Target = target;
        DraftEndPoint = draftEndPoint;

        this.WhenAnyValue(edge => edge.DraftEndPoint)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(EndPoint)));
    }

    /// <summary>
    /// 初始化正式连线。
    /// </summary>
    /// <param name="id">连线稳定标识。</param>
    /// <param name="source">输出端源端口。</param>
    /// <param name="target">输入端目标端口。</param>
    public EdgeViewModel(Guid id, PortViewModel source, PortViewModel target)
        : this(id, source, target, target.AnchorPoint)
    {
        if (!source.CanConnectTo(target))
        {
            throw new ArgumentException("源端口和目标端口不兼容。", nameof(target));
        }
    }

    /// <summary>
    /// 连线稳定标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 输出端源端口。
    /// </summary>
    public PortViewModel Source { get; }

    /// <summary>
    /// 输入端目标端口；草稿连线没有目标端口。
    /// </summary>
    public PortViewModel? Target { get; }

    /// <summary>
    /// 草稿连线当前终点。
    /// </summary>
    [Reactive]
    public Point DraftEndPoint { get; set; }

    /// <summary>
    /// 是否为拖拽中的草稿连线。
    /// </summary>
    public bool IsDraft => Target is null;

    /// <summary>
    /// 连线起点。
    /// </summary>
    public Point StartPoint => Source.AnchorPoint;

    /// <summary>
    /// 连线终点。
    /// </summary>
    public Point EndPoint => Target?.AnchorPoint ?? DraftEndPoint;

    /// <summary>
    /// 创建拖拽中的草稿连线。
    /// </summary>
    /// <param name="source">输出端源端口。</param>
    /// <param name="currentPoint">当前指针位置。</param>
    /// <returns>草稿连线。</returns>
    public static EdgeViewModel CreateDraft(PortViewModel source, Point currentPoint)
    {
        return new EdgeViewModel(Guid.NewGuid(), source, null, currentPoint);
    }
}
