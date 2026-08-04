using FlowForge.App.ViewModels;
using FlowForge.App.Security;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Serialization;
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
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ISecretStore>(_ => SecretStoreFactory.CreateDefault());
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddSingleton<NodeRegistry>(provider => NodeRegistry.CreateDefault(
            provider.GetRequiredService<ISecretStore>(),
            provider.GetRequiredService<HttpClient>()));
        services.AddSingleton<IWorkflowRunner, WorkflowRunner>();
        services.AddSingleton<IStage3WorkflowRunner, Stage3SampleWorkflowRunner>();
        services.AddSingleton<IWorkflowFileService, AvaloniaWorkflowFileService>();
        services.AddSingleton<MainWindowViewModel>(provider => new MainWindowViewModel(
            provider.GetRequiredService<IWorkflowRunner>(),
            provider.GetRequiredService<IWorkflowFileService>(),
            provider.GetRequiredService<NodeRegistry>(),
            provider.GetRequiredService<ISecretStore>(),
            provider.GetRequiredService<IFilePickerService>()));
        services.AddTransient<MainWindow>();

        return services;
    }
}
