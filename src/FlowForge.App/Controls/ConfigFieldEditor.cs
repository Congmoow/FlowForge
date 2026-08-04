using System.Globalization;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Controls;

/// <summary>
/// 根据 ConfigField 元数据承载六类配置编辑器的属性面板控件。
/// </summary>
public sealed class ConfigFieldEditor : UserControl
{
    /// <summary>
    /// 获取控件支持的编辑器名称。
    /// </summary>
    public static IReadOnlyList<string> SupportedEditors { get; } = Array.AsReadOnly(
        new[] { "TextBox", "NumericUpDown", "ComboBox", "FilePicker", "MultilineText", "Password" });

    /// <summary>
    /// 当前字段属性。
    /// </summary>
    public static readonly StyledProperty<ConfigFieldViewModel?> FieldProperty =
        AvaloniaProperty.Register<ConfigFieldEditor, ConfigFieldViewModel?>(nameof(Field));

    /// <summary>
    /// 获取或设置当前字段。
    /// </summary>
    public ConfigFieldViewModel? Field
    {
        get => GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    private ConfigFieldViewModel? observedField;
    private Control? editor;
    private bool isSynchronizing;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FieldProperty)
        {
            if (observedField is not null)
            {
                observedField.PropertyChanged -= OnFieldPropertyChanged;
            }

            observedField = change.NewValue as ConfigFieldViewModel;
            editor = BuildEditor(observedField);
            Content = editor;
            if (observedField is not null)
            {
                observedField.PropertyChanged += OnFieldPropertyChanged;
            }
        }
    }

    private Control? BuildEditor(ConfigFieldViewModel? field)
    {
        if (field is null)
        {
            return null;
        }

        return field.Editor switch
        {
            "TextBox" => BuildTextBox(field, multiline: false),
            "MultilineText" => BuildTextBox(field, multiline: true),
            "NumericUpDown" => BuildNumericBox(field),
            "ComboBox" => BuildComboBox(field),
            "FilePicker" => BuildFilePicker(field),
            "Password" => BuildPasswordBox(field),
            _ => BuildTextBox(field, multiline: false),
        };
    }

    private TextBox BuildTextBox(ConfigFieldViewModel field, bool multiline)
    {
        var textBox = new TextBox
        {
            Text = Convert.ToString(field.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 84 : 0,
        };
        textBox.LostFocus += (_, _) =>
        {
            if (!isSynchronizing)
            {
                _ = field.CommitAsync(textBox.Text);
            }
        };
        return textBox;
    }

    private NumericUpDown BuildNumericBox(ConfigFieldViewModel field)
    {
        var numeric = new NumericUpDown { Value = ToDecimal(field.Value) };
        if (field.Descriptor.Min is { } min)
        {
            numeric.Minimum = (decimal)min;
        }

        if (field.Descriptor.Max is { } max)
        {
            numeric.Maximum = (decimal)max;
        }
        numeric.ValueChanged += (_, args) =>
        {
            if (!isSynchronizing)
            {
                _ = field.CommitAsync(args.NewValue);
            }
        };
        return numeric;
    }

    private ComboBox BuildComboBox(ConfigFieldViewModel field)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = field.Options,
            SelectedItem = Convert.ToString(field.Value, CultureInfo.InvariantCulture),
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (!isSynchronizing)
            {
                _ = field.CommitAsync(comboBox.SelectedItem);
            }
        };
        return comboBox;
    }

    private static StackPanel BuildFilePicker(ConfigFieldViewModel field)
    {
        var textBox = new TextBox
        {
            Text = Convert.ToString(field.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            IsReadOnly = true,
        };
        var button = new Button { Content = "选择…", HorizontalAlignment = HorizontalAlignment.Right };
        button.Click += async (_, _) => await field.PickFileAsync();
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { textBox, button },
        };
    }

    private static TextBox BuildPasswordBox(ConfigFieldViewModel field)
    {
        var textBox = new TextBox
        {
            PasswordChar = '●',
            Watermark = "输入密钥",
        };
        textBox.LostFocus += async (_, _) =>
        {
            await field.CommitAsync(textBox.Text);
            textBox.Clear();
        };
        return textBox;
    }

    private static decimal? ToDecimal(object? value)
    {
        return value is null
            ? null
            : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is ConfigFieldViewModel field
            && string.Equals(args.PropertyName, nameof(ConfigFieldViewModel.Value), StringComparison.Ordinal))
        {
            SynchronizeEditorValue(field);
        }
    }

    private void SynchronizeEditorValue(ConfigFieldViewModel field)
    {
        if (isSynchronizing || field.IsPassword)
        {
            return;
        }

        isSynchronizing = true;
        try
        {
            switch (editor)
            {
                case TextBox textBox:
                    textBox.Text = ToText(field.Value);
                    break;
                case NumericUpDown numeric:
                    numeric.Value = ToDecimal(field.Value);
                    break;
                case ComboBox comboBox:
                    comboBox.SelectedItem = Convert.ToString(field.Value, CultureInfo.InvariantCulture);
                    break;
                case StackPanel { Children.Count: > 0 } filePicker
                    when filePicker.Children[0] is TextBox filePath:
                    filePath.Text = ToText(field.Value);
                    break;
            }
        }
        finally
        {
            isSynchronizing = false;
        }
    }

    private static string ToText(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
