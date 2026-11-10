using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SpectroscopicAnalysis.Host.ViewModels;

/// <summary>侧栏导航项（框架阶段：标题 + 目标 ViewModel + 选中态）。</summary>
public sealed partial class NavItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required string Title { get; init; }

    public required ObservableObject Target { get; init; }
}

/// <summary>
/// 主窗口 ViewModel：导航切换 + 缩放档位（界面规范 5.3：100%/125%/150%）。
/// 严格 MVVM：不引用任何 WPF 类型（开发规范 3.3）。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>缩放档位候选（界面规范 5.3）。</summary>
    public static readonly IReadOnlyList<double> ScaleOptions = new[] { 1.0, 1.25, 1.5 };

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNav;

    /// <summary>界面缩放系数（1.0/1.25/1.5），根容器 LayoutTransform 绑定。</summary>
    [ObservableProperty]
    private double _uiScale = 1.0;

    /// <summary>导航项（静态六项，无需变更通知）。</summary>
    public ReadOnlyObservableCollection<NavItem> NavItems { get; }

    public MainViewModel(
        DashboardViewModel dashboard,
        SamplesViewModel samples,
        SctViewModel sct,
        CoefficientsViewModel coefficients,
        AlarmsViewModel alarms,
        SettingsViewModel settings)
    {
        var items = new ObservableCollection<NavItem>
        {
            new() { Title = "主控台 · 生产监控", Target = dashboard },
            new() { Title = "样品档案", Target = samples },
            new() { Title = "SCT 管理", Target = sct },
            new() { Title = "系数管理", Target = coefficients },
            new() { Title = "日志报警", Target = alarms },
            new() { Title = "设置", Target = settings },
        };
        NavItems = new ReadOnlyObservableCollection<NavItem>(items);

        // 默认落在主控台
        NavigateTo(items[0]);
    }

    /// <summary>导航命令（页面跳转属框架行为，业务逻辑在各页面 VM 内）。</summary>
    [RelayCommand]
    private void NavigateTo(NavItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (SelectedNav is not null)
        {
            SelectedNav.IsSelected = false;
        }

        CurrentViewModel = item.Target;
        SelectedNav = item;
        item.IsSelected = true;
    }

    /// <summary>切换缩放档位（XAML CommandParameter 为字符串，命令侧解析；非法值忽略）。</summary>
    [RelayCommand]
    private void SetScale(string? scaleText)
    {
        if (double.TryParse(scaleText, out var scale) && ScaleOptions.Contains(scale))
        {
            UiScale = scale;
        }
    }
}
