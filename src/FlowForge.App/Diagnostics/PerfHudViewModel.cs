using System.Globalization;
using ReactiveUI;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 以采样窗口为更新粒度的性能 HUD 状态。
/// </summary>
public sealed class PerfHudViewModel : ReactiveObject, IDisposable
{
    private bool isVisible;
    private int nodeCount;
    private double framesPerSecond;
    private long workingSetBytes;
    private string phase = "未运行";
    private string errorMessage = string.Empty;
    private bool showHud = true;
    private bool disposed;

    /// <summary>HUD 当前是否可见。</summary>
    public bool IsVisible
    {
        get => isVisible;
        private set => this.RaiseAndSetIfChanged(ref isVisible, value);
    }

    /// <summary>当前性能场景节点数。</summary>
    public int NodeCount
    {
        get => nodeCount;
        private set => this.RaiseAndSetIfChanged(ref nodeCount, value);
    }

    /// <summary>最近一个采样窗口的 FPS。</summary>
    public double FramesPerSecond
    {
        get => framesPerSecond;
        private set => this.RaiseAndSetIfChanged(ref framesPerSecond, value);
    }

    /// <summary>最近一个采样窗口的 WorkingSet64 字节数。</summary>
    public long WorkingSetBytes
    {
        get => workingSetBytes;
        private set => this.RaiseAndSetIfChanged(ref workingSetBytes, value);
    }

    /// <summary>当前性能运行阶段。</summary>
    public string Phase
    {
        get => phase;
        private set => this.RaiseAndSetIfChanged(ref phase, value);
    }

    /// <summary>最近一次运行错误；没有错误时为空。</summary>
    public string ErrorMessage
    {
        get => errorMessage;
        private set => this.RaiseAndSetIfChanged(ref errorMessage, value);
    }

    /// <summary>供覆盖层显示的格式化文本。</summary>
    public string DisplayText
    {
        get
        {
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "性能模式\nFPS {0:F1}\n节点 {1}\nWorkingSet64 {2} B\n{3}",
                FramesPerSecond,
                NodeCount,
                WorkingSetBytes,
                Phase);
            return string.IsNullOrWhiteSpace(ErrorMessage)
                ? text
                : string.Concat(text, "\n", ErrorMessage);
        }
    }

    /// <summary>
    /// 开始一次性能运行，并显示预热阶段。
    /// </summary>
    /// <param name="options">性能运行参数。</param>
    public void Start(PerfRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ThrowIfDisposed();

        NodeCount = options.NodeCount;
        FramesPerSecond = 0;
        WorkingSetBytes = 0;
        ErrorMessage = string.Empty;
        Phase = $"预热 {options.Warmup.TotalSeconds:0.#} 秒";
        showHud = options.ShowHud;
        IsVisible = options.ShowHud;
        RaiseDisplayTextChanged();
    }

    /// <summary>
    /// 应用一个正式采样窗口的结果。调用方应按采样间隔调用，而不是按帧调用。
    /// </summary>
    /// <param name="sample">实际画布采样结果。</param>
    public void ApplySample(PerfSample sample)
    {
        ThrowIfDisposed();

        NodeCount = sample.NodeCount;
        FramesPerSecond = sample.FramesPerSecond;
        WorkingSetBytes = sample.WorkingSetBytes;
        Phase = "采样中";
        ErrorMessage = string.Empty;
        RaiseDisplayTextChanged();
    }

    /// <summary>
    /// 显示完整运行的最终聚合结果。
    /// </summary>
    /// <param name="result">实际采样运行结果。</param>
    public void Complete(PerfRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ThrowIfDisposed();

        NodeCount = result.NodeCount;
        FramesPerSecond = result.AverageFramesPerSecond;
        WorkingSetBytes = result.PeakWorkingSetBytes;
        Phase = "采样完成";
        ErrorMessage = string.Empty;
        IsVisible = showHud;
        RaiseDisplayTextChanged();
    }

    /// <summary>
    /// 在无人值守运行失败时保留可见错误状态。
    /// </summary>
    /// <param name="error">运行失败异常。</param>
    public void Fail(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        ThrowIfDisposed();

        Phase = "采样失败";
        ErrorMessage = error.Message;
        IsVisible = showHud;
        RaiseDisplayTextChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        IsVisible = false;
        GC.SuppressFinalize(this);
    }

    private void RaiseDisplayTextChanged()
    {
        this.RaisePropertyChanged(nameof(DisplayText));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
