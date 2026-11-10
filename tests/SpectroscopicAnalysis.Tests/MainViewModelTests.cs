using CommunityToolkit.Mvvm.ComponentModel;
using SpectroscopicAnalysis.Host.ViewModels;

namespace SpectroscopicAnalysis.Tests;

/// <summary>主窗口 ViewModel 测试：导航切换与缩放档位（开发计划 D2 验收项）。</summary>
public sealed class MainViewModelTests
{
    private static MainViewModel CreateMain() => new(
        new DashboardViewModel(),
        new SamplesViewModel(new FakeSampleRepository(), new FakeUiDispatcher()),
        new SctViewModel(),
        new CoefficientsViewModel(),
        new AlarmsViewModel(),
        new SettingsViewModel());

    [Fact]
    public void 构造后_默认落在主控台且首项选中()
    {
        var main = CreateMain();

        Assert.IsType<DashboardViewModel>(main.CurrentViewModel);
        Assert.NotNull(main.SelectedNav);
        Assert.Equal("主控台 · 生产监控", main.SelectedNav.Title);
        Assert.True(main.NavItems[0].IsSelected);
    }

    [Fact]
    public void NavigateTo_切换页面并维护选中态()
    {
        var main = CreateMain();
        var target = main.NavItems.First(n => n.Title == "SCT 管理");

        main.NavigateToCommand.Execute(target);

        Assert.IsType<SctViewModel>(main.CurrentViewModel);
        Assert.True(target.IsSelected);
        Assert.False(main.NavItems[0].IsSelected); // 原选中项取消
    }

    [Fact]
    public void NavigateTo_空参数不抛异常()
    {
        var main = CreateMain();
        main.NavigateToCommand.Execute(null); // 不应抛异常，状态不变
        Assert.IsType<DashboardViewModel>(main.CurrentViewModel);
    }

    [Theory]
    [InlineData("1.0", 1.0)]
    [InlineData("1.25", 1.25)]
    [InlineData("1.5", 1.5)]
    public void SetScale_合法档位生效(string scaleText, double expected)
    {
        var main = CreateMain();
        main.SetScaleCommand.Execute(scaleText);
        Assert.Equal(expected, main.UiScale);
    }

    [Theory]
    [InlineData("2.0")]   // 不在档位表
    [InlineData("abc")]   // 非数字
    [InlineData(null)]    // 空参数
    public void SetScale_非法档位被拒绝(string? scaleText)
    {
        var main = CreateMain();
        main.SetScaleCommand.Execute(scaleText);
        Assert.Equal(1.0, main.UiScale);   // 保持原值
    }
}
