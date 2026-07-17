using FlowForge.App.ViewModels;
using FlowForge.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.App.Services;

/// <summary>
/// 应用层依赖注入注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 FlowForge 桌面应用的基础服务。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>注册后的服务集合。</returns>
    public static IServiceCollection AddFlowForgeApp(this IServiceCollection services)
    {
        services.AddSingleton<WorkflowScheduler>();
        services.AddSingleton<IStage3WorkflowRunner, Stage3SampleWorkflowRunner>();
        services.AddSingleton<IWorkflowFileService, AvaloniaWorkflowFileService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
