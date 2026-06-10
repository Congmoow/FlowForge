using Avalonia.Controls;
using FlowForge.App.ViewModels;

namespace FlowForge.App;

/// <summary>
/// FlowForge 主窗口。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 创建供 XAML 设计器使用的主窗口实例。
    /// </summary>
    public MainWindow()
        : this(new MainWindowViewModel())
    {
    }

    /// <summary>
    /// 创建绑定到根 ViewModel 的主窗口实例。
    /// </summary>
    /// <param name="viewModel">主窗口根 ViewModel。</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
