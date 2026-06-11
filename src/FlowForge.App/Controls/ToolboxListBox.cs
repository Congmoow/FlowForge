using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Controls;

/// <summary>
/// 支持从节点库拖拽节点模板的列表控件。
/// </summary>
public sealed class ToolboxListBox : ListBox
{
    /// <summary>
    /// 拖拽数据格式。
    /// </summary>
    public const string DragNodeTemplateFormat = "application/x-flowforge-node-template";

    private Point dragStartPoint;
    private NodeTemplateViewModel? dragTemplate;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        dragStartPoint = e.GetPosition(this);
        dragTemplate = FindTemplateFromSource(e.Source);
    }

    /// <inheritdoc />
    protected override async void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (dragTemplate is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            dragTemplate = null;
            return;
        }

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - dragStartPoint;
        if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
        {
            return;
        }

        var data = new DataObject();
        data.Set(DragNodeTemplateFormat, dragTemplate);
        dragTemplate = null;
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy).ConfigureAwait(true);
    }

    private static NodeTemplateViewModel? FindTemplateFromSource(object? source)
    {
        return source switch
        {
            StyledElement { DataContext: NodeTemplateViewModel template } => template,
            _ => null,
        };
    }
}
