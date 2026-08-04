using System.Globalization;
using System.Reflection;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Serialization;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 提供一个 immutable 节点配置的反射字段描述和构造新配置能力。
/// </summary>
public sealed class ConfigDescriptor
{
    private ConfigDescriptor(Type configType, IReadOnlyList<ConfigFieldDescriptor> fields)
    {
        ConfigType = configType;
        Fields = fields;
    }

    /// <summary>
    /// 获取配置运行时类型。
    /// </summary>
    public Type ConfigType { get; }

    /// <summary>
    /// 获取按显示顺序排列的配置字段。
    /// </summary>
    public IReadOnlyList<ConfigFieldDescriptor> Fields { get; }

    /// <summary>
    /// 从配置类型的公共属性和 <see cref="ConfigFieldAttribute"/> 创建描述。
    /// </summary>
    /// <param name="configType">实现 <see cref="INodeConfig"/> 的配置类型。</param>
    /// <returns>反射字段描述。</returns>
    public static ConfigDescriptor Create(Type configType)
    {
        ArgumentNullException.ThrowIfNull(configType);
        if (!typeof(INodeConfig).IsAssignableFrom(configType))
        {
            throw new ArgumentException(
                $"配置类型必须实现 {nameof(INodeConfig)}。",
                nameof(configType));
        }

        var fields = configType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => (Property: property, Attribute: property.GetCustomAttribute<ConfigFieldAttribute>()))
            .Where(item => item.Attribute is not null && item.Property.CanRead)
            .Select(item => new ConfigFieldDescriptor(item.Property, item.Attribute!))
            .OrderBy(field => field.Order)
            .ThenBy(field => field.PropertyName, StringComparer.Ordinal)
            .ToArray();
        return new ConfigDescriptor(configType, Array.AsReadOnly(fields));
    }

    /// <summary>
    /// 按属性名查找字段描述。
    /// </summary>
    /// <param name="propertyName">配置属性名。</param>
    /// <param name="field">找到的字段。</param>
    /// <returns>找到时返回 <see langword="true"/>。</returns>
    public bool TryGetField(string propertyName, out ConfigFieldDescriptor? field)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            field = null;
            return false;
        }

        field = Fields.FirstOrDefault(item =>
            string.Equals(item.PropertyName, propertyName, StringComparison.Ordinal));
        return field is not null;
    }

    /// <summary>
    /// 使用一个字段的新值构造新的 typed immutable 配置。
    /// </summary>
    /// <param name="config">当前配置实例。</param>
    /// <param name="propertyName">要替换的属性名。</param>
    /// <param name="rawValue">来自编辑器的原始值。</param>
    /// <returns>新的配置实例；原实例不会被修改。</returns>
    /// <exception cref="ConfigValidationException">属性不存在、类型转换或字段校验失败。</exception>
    public INodeConfig CreateUpdatedConfig(INodeConfig config, string propertyName, object? rawValue)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!ConfigType.IsInstanceOfType(config))
        {
            throw new ConfigValidationException($"配置类型 {config.GetType().Name} 与 {ConfigType.Name} 不匹配。");
        }

        if (!TryGetField(propertyName, out var field) || field is null)
        {
            throw new ConfigValidationException($"配置属性 {propertyName} 不存在或未声明编辑元数据。");
        }

        var value = ConvertValue(field, rawValue);
        var constructor = ConfigType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        if (constructor is null)
        {
            throw new ConfigValidationException($"配置类型 {ConfigType.Name} 没有公共构造函数。");
        }

        var arguments = constructor.GetParameters().Select(parameter =>
        {
            var property = ConfigType.GetProperty(
                parameter.Name!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null)
            {
                throw new ConfigValidationException(
                    $"配置构造参数 {parameter.Name} 没有对应属性。");
            }

            var currentValue = string.Equals(property.Name, field.PropertyName, StringComparison.Ordinal)
                ? value
                : property.GetValue(config);
            return ConvertToType(currentValue, parameter.ParameterType, parameter.Name!);
        }).ToArray();

        try
        {
            return (INodeConfig)constructor.Invoke(arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new ConfigValidationException(
                $"创建配置 {ConfigType.Name} 失败：{exception.InnerException.Message}",
                exception.InnerException);
        }
    }

    private static object? ConvertValue(ConfigFieldDescriptor field, object? rawValue)
    {
        if (rawValue is JsonElement element)
        {
            rawValue = element.Deserialize(field.PropertyType, WorkflowJsonSerializerOptions.Default);
        }

        if (rawValue is string text && field.Required && string.IsNullOrWhiteSpace(text))
        {
            throw new ConfigValidationException($"属性 {field.PropertyName} 不能为空。");
        }

        var value = ConvertToType(rawValue, field.PropertyType, field.PropertyName);
        if (value is not null && (field.Min is not null || field.Max is not null))
        {
            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (field.Min is not null && number < field.Min.Value)
            {
                throw new ConfigValidationException($"属性 {field.PropertyName} 不能小于 {field.Min}。");
            }

            if (field.Max is not null && number > field.Max.Value)
            {
                throw new ConfigValidationException($"属性 {field.PropertyName} 不能大于 {field.Max}。");
            }
        }

        return value;
    }

    private static object? ConvertToType(object? value, Type targetType, string propertyName)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;
        if (value is null)
        {
            if (effectiveType.IsValueType && nullableType is null)
            {
                throw new ConfigValidationException($"属性 {propertyName} 不能为空。");
            }

            return null;
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            if (effectiveType.IsEnum)
            {
                return value is string text
                    ? Enum.Parse(effectiveType, text, ignoreCase: true)
                    : Enum.ToObject(effectiveType, value);
            }

            if (effectiveType == typeof(char) && value is string character)
            {
                if (character.Length != 1)
                {
                    throw new FormatException("字符必须只有一个符号。");
                }

                return character[0];
            }

            if (effectiveType == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new ConfigValidationException(
                $"属性 {propertyName} 的值无法转换为 {effectiveType.Name}。",
                exception);
        }
    }
}

/// <summary>
/// 描述一个可编辑配置属性。
/// </summary>
public sealed class ConfigFieldDescriptor
{
    internal ConfigFieldDescriptor(PropertyInfo property, ConfigFieldAttribute attribute)
    {
        Property = property;
        PropertyName = property.Name;
        PropertyType = property.PropertyType;
        Label = string.IsNullOrWhiteSpace(attribute.Label) ? property.Name : attribute.Label;
        Editor = string.IsNullOrWhiteSpace(attribute.Editor) ? "TextBox" : attribute.Editor;
        Options = (attribute.Options ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Required = attribute.Required;
        Min = double.IsNaN(attribute.Min) ? null : attribute.Min;
        Max = double.IsNaN(attribute.Max) ? null : attribute.Max;
        Order = attribute.Order;
    }

    /// <summary>获取反射属性信息。</summary>
    public PropertyInfo Property { get; }

    /// <summary>获取配置属性名。</summary>
    public string PropertyName { get; }

    /// <summary>获取属性面板标签。</summary>
    public string Label { get; }

    /// <summary>获取编辑器类型名称。</summary>
    public string Editor { get; }

    /// <summary>获取属性值类型。</summary>
    public Type PropertyType { get; }

    /// <summary>获取 ComboBox 选项。</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>获取是否必填。</summary>
    public bool Required { get; }

    /// <summary>获取数值下限。</summary>
    public double? Min { get; }

    /// <summary>获取数值上限。</summary>
    public double? Max { get; }

    /// <summary>获取显示顺序。</summary>
    public int Order { get; }

    /// <summary>
    /// 从配置实例读取字段值。
    /// </summary>
    /// <param name="config">配置实例。</param>
    /// <returns>字段值。</returns>
    public object? GetValue(INodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Property.GetValue(config);
    }
}

/// <summary>
/// 表示属性面板配置编辑失败。
/// </summary>
public sealed class ConfigValidationException : Exception
{
    /// <summary>
    /// 初始化配置校验异常。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public ConfigValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化带内部异常的配置校验异常。
    /// </summary>
    /// <param name="message">错误信息。</param>
    /// <param name="innerException">原始异常。</param>
    public ConfigValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
