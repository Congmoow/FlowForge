namespace FlowForge.App.Security;

/// <summary>
/// 表示无需 Shell 解析的外部进程调用。
/// </summary>
/// <param name="FileName">要启动的可执行文件名或路径。</param>
/// <param name="Arguments">按顺序传递给可执行文件的参数。</param>
/// <param name="StandardInput">可选的标准输入内容。</param>
public sealed record ProcessCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? StandardInput = null);

/// <summary>
/// 表示外部进程执行后的结果。
/// </summary>
/// <param name="ExitCode">进程退出代码。</param>
/// <param name="StandardOutput">进程标准输出。</param>
/// <param name="StandardError">进程标准错误。</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// 提供可替换的外部进程执行能力。
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// 执行指定的外部进程调用。
    /// </summary>
    /// <param name="command">要执行的命令。</param>
    /// <param name="cancellationToken">用于取消进程执行的令牌。</param>
    /// <returns>进程的退出代码和输出。</returns>
    Task<ProcessResult> RunAsync(ProcessCommand command, CancellationToken cancellationToken = default);
}
