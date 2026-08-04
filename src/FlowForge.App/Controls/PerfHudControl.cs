using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using FlowForge.App.Diagnostics;

namespace FlowForge.App.Controls;

/// <summary>
/// 显示开发模式 FPS、节点数和 WorkingSet64 的透明覆盖层。
/// </summary>
public sealed class PerfHudControl : Border
{
    /// <summary>
    /// 初始化性能 HUD。状态绑定只在采样窗口更新，不订阅画布每帧事件。
    /// </summary>
    public PerfHudControl()
    {
        Background = new SolidColorBrush(Color.Parse("#E6F1F8FF"));
        BorderBrush = new SolidColorBrush(Color.Parse("#8094A9BF"));
        BorderThickness = new Avalonia.Thickness(1);
        CornerRadius = new Avalonia.CornerRadius(6);
        Padding = new Avalonia.Thickness(10, 8);
        IsHitTestVisible = false;
        IsVisible = false;

        var presenter = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#172033")),
            FontFamily = "Consolas, Menlo, monospace",
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
        };
        presenter.Bind(TextBlock.TextProperty, new Binding(nameof(PerfHudViewModel.DisplayText)));
        Bind(IsVisibleProperty, new Binding(nameof(PerfHudViewModel.IsVisible)));
        Child = presenter;
    }
}
