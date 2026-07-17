using System.Diagnostics;

namespace FlowForge.App.Security;

/// <summary>
/// 使用 <see cref="Process"/> 执行外部命令。
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(ProcessCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FileName);

        var startInfo = new ProcessStartInfo(command.FileName)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardInput = command.StandardInput is not null,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"无法启动外部命令 {command.FileName}。");
        }

        using var registration = cancellationToken.Register(static state =>
        {
            var runningProcess = (Process)state!;
            if (!runningProcess.HasExited)
            {
                runningProcess.Kill(entireProcessTree: true);
            }
        }, process);

        if (command.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(command.StandardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}
