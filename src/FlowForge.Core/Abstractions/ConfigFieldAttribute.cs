namespace FlowForge.Core.Abstractions;

/// <summary>
/// 描述 immutable 节点配置属性在属性面板中的显示和编辑方式。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ConfigFieldAttribute : Attribute
{
    /// <summary>
    /// 获取或设置属性面板显示标签。
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置编辑器类型名称。
    /// </summary>
    public string Editor { get; init; } = "TextBox";

    /// <summary>
    /// 获取或设置 ComboBox 选项，以逗号分隔。
    /// </summary>
    public string? Options { get; init; }

    /// <summary>
    /// 获取或设置该字段是否必须有非空值。
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// 获取或设置数值下限；NaN 表示不限制。
    /// </summary>
    public double Min { get; init; } = double.NaN;

    /// <summary>
    /// 获取或设置数值上限；NaN 表示不限制。
    /// </summary>
    public double Max { get; init; } = double.NaN;

    /// <summary>
    /// 获取或设置字段在面板中的显示顺序。
    /// </summary>
    public int Order { get; init; } = int.MaxValue;
}
