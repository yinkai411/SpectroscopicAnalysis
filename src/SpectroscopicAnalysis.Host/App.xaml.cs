using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
// 命名空间尾段 Host 会遮蔽 M.E.Hosting 的 Host 类，起别名引用
using HostApp = Microsoft.Extensions.Hosting.Host;

namespace SpectroscopicAnalysis.Host;

/// <summary>
/// 应用入口：Generic Host + DI 骨架（开发计划 D1）。
/// 依赖一律经构造注入；日志统一走 Serilog（结构化，后续接入 LoggingAlarm 错误链）。
/// </summary>
public partial class App
{
    private readonly IHost _host;

    public App()
    {
        _host = HostApp.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<MainWindow>();
            })
            .UseSerilog((context, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .WriteTo.File("logs/host-.log", rollingInterval: RollingInterval.Day);
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host.Start();
        Log.Information("上位机启动（{Mode}）", "Generic Host");

        // 主窗口由 DI 提供，后续页面/ViewModel 均按此模式注册
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Host 停止为同步引导期收尾（WPF OnExit 无异步重载），业务代码中禁止此类阻塞写法
        _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        _host.Dispose();
        Log.Information("上位机退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
