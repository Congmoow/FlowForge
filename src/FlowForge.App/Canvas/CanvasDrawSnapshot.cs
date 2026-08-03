using System.Collections.Immutable;
using Avalonia;
using Avalonia.Media;

namespace FlowForge.App.Canvas;

/// <summary>节点绘制所需的不可变快照。</summary>
public sealed record NodeDrawSnapshot(
    Guid Id,
    Rect Bounds,
    string Title,
    string Body,
    Color BorderColor,
    bool IsSelected,
    ImmutableArray<Point> PortPoints);

/// <summary>连线绘制所需的不可变快照。</summary>
public sealed record EdgeDrawSnapshot(
    Guid Id,
    Point StartPoint,
    Point EndPoint,
    double StrokeWidth);
