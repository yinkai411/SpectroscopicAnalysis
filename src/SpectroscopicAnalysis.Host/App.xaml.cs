using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SpectroscopicAnalysis.Host.Data;
using SpectroscopicAnalysis.Host.Data.Database;
using SpectroscopicAnalysis.Host.Data.Repositories;
// 命名空间尾段 Host 会遮蔽 M.E.Hosting 的 Host 类，起别名引用
using HostApp = Microsoft.Extensions.Hosting.Host;

namespace SpectroscopicAnalysis.Host;

/// <summary>
/// 应用入口：Generic Host + DI 骨架（开发计划 D1/D3）。
/// 依赖一律经构造注入；日志统一走 Serilog；数据库启动时自动建库（WAL + 外键）。
/// </summary>
public partial class App
{
    private readonly IHost _host;

    public App()
    {
        _host = HostApp.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 数据库文件路径：配置的相对路径基于应用基目录解析（Data/ 不入库，见 .gitignore）
                var dbPath = Path.Combine(AppContext.BaseDirectory,
                    context.Configuration["Database:Path"] ?? Path.Combine("Data", "spectro.db"));

                services.AddSingleton(new Db(dbPath));
                services.AddSingleton<DbInitializer>();
                services.AddSingleton<ISctSampleRepository, SctSampleRepository>();
                services.AddSingleton<SeedData>();

                // UI 层：调度抽象（MVVM 分层要求）+ 页面 ViewModel（单例，导航共享状态）
                services.AddSingleton<Ui.IUiDispatcher, Ui.UiDispatcher>();
                services.AddSingleton<ViewModels.DashboardViewModel>();
                services.AddSingleton<ViewModels.SamplesViewModel>();
                services.AddSingleton<ViewModels.SctViewModel>();
                services.AddSingleton<ViewModels.CoefficientsViewModel>();
                services.AddSingleton<ViewModels.AlarmsViewModel>();
                services.AddSingleton<ViewModels.SettingsViewModel>();
                services.AddSingleton<ViewModels.MainViewModel>();
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

        // 全局异常兜底（开发规范 11.2 最小版；完整错误链随 D9 落地）——启动期先记录再终止
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Fatal(ex.Exception, "UI 线程未处理异常");
            Log.CloseAndFlush();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            Log.Fatal(ex.ExceptionObject as Exception, "非托管/后台线程未处理异常");
            Log.CloseAndFlush();
        };

        _host.Start();

        // 建库 + 种子（幂等）；失败走统一错误链前先记录并让异常上抛终止启动（启动期硬失败）
        var dbInitializer = _host.Services.GetRequiredService<DbInitializer>();
        dbInitializer.EnsureCreated();
        var seeded = _host.Services.GetRequiredService<SeedData>();
        seeded.SeedIfEmpty();
        Log.Information("数据库就绪（建表/种子幂等完成）");

        // 主窗口由 DI 提供；DataContext = MainViewModel（组合根装配，View 零业务）
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _host.Services.GetRequiredService<ViewModels.MainViewModel>();
        mainWindow.Show();
        Log.Information("上位机启动完成");
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
