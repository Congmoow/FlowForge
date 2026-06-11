using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace FlowForge.App.Controls;

/// <summary>
/// 用三次贝塞尔曲线绘制节点连线。
/// </summary>
public sealed class BezierEdgeShape : Shape
{
    private const double MinimumHandleLength = 48;

    /// <summary>
    /// 起点属性。
    /// </summary>
    public static readonly StyledProperty<Point> StartPointProperty =
        AvaloniaProperty.Register<BezierEdgeShape, Point>(nameof(StartPoint));

    /// <summary>
    /// 终点属性。
    /// </summary>
    public static readonly StyledProperty<Point> EndPointProperty =
        AvaloniaProperty.Register<BezierEdgeShape, Point>(nameof(EndPoint));

    /// <summary>
    /// 连线起点。
    /// </summary>
    public Point StartPoint
    {
        get => GetValue(StartPointProperty);
        set => SetValue(StartPointProperty, value);
    }

    /// <summary>
    /// 连线终点。
    /// </summary>
    public Point EndPoint
    {
        get => GetValue(EndPointProperty);
        set => SetValue(EndPointProperty, value);
    }

    /// <summary>
    /// 根据起点和终点计算贝塞尔控制点。
    /// </summary>
    /// <param name="startPoint">起点。</param>
    /// <param name="endPoint">终点。</param>
    /// <returns>第一和第二控制点。</returns>
    public static (Point First, Point Second) CalculateControlPoints(Point startPoint, Point endPoint)
    {
        var handleLength = Math.Max(Math.Abs(endPoint.X - startPoint.X) / 2, MinimumHandleLength);
        return (
            new Point(startPoint.X + handleLength, startPoint.Y),
            new Point(endPoint.X - handleLength, endPoint.Y));
    }

    /// <summary>
    /// 创建贝塞尔连线几何。
    /// </summary>
    /// <param name="startPoint">起点。</param>
    /// <param name="endPoint">终点。</param>
    /// <returns>可绘制几何。</returns>
    public static PathGeometry CreateGeometry(Point startPoint, Point endPoint)
    {
        var (firstControlPoint, secondControlPoint) = CalculateControlPoints(startPoint, endPoint);

        return new PathGeometry
        {
            Figures =
            [
                new PathFigure
                {
                    StartPoint = startPoint,
                    IsClosed = false,
                    IsFilled = false,
                    Segments =
                    [
                        new BezierSegment
                        {
                            Point1 = firstControlPoint,
                            Point2 = secondControlPoint,
                            Point3 = endPoint,
                        },
                    ],
                },
            ],
        };
    }

    /// <inheritdoc />
    protected override Geometry CreateDefiningGeometry()
    {
        return CreateGeometry(StartPoint, EndPoint);
    }
}
