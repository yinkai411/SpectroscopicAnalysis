using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;
using SpectroscopicAnalysis.Host.Ui;

namespace SpectroscopicAnalysis.Host.ViewModels;

/// <summary>
/// 台账行展示模型：领域数据 → 展示文本映射（开发规范 3.3：VM 负责映射为展示模型）。
/// SampleTypeKey 为类型键（筛选用，非绑定字段）；Status 为原始枚举串（视图 DataTrigger 切换徽章颜色）。
/// </summary>
public sealed class SampleRowItem
{
    public required string SampleId { get; init; }
    public required string SampleTypeKey { get; init; }
    public required string TypeDisplay { get; init; }
    public string? AlloyType { get; init; }
    public string? RackPosition { get; init; }
    public required string ThicknessText { get; init; }
    public required string MinThicknessText { get; init; }
    public required string UsageText { get; init; }
    public required string Status { get; init; }
    public required string StatusText { get; init; }
    public required string LastHeightText { get; init; }
}

/// <summary>
/// 样品档案 ViewModel：从 SQLite（SCT_Sample）加载样品并按类型筛选展示。
/// 数据库 I/O 经 Task.Run + IUiDispatcher 回 UI 线程（I/O 不阻塞 UI，开发规范第 4 节）。
/// </summary>
public sealed partial class SamplesViewModel : ObservableObject, IPageLifecycle
{
    public const string FilterAll = "All";

    private readonly ISctSampleRepository _samples;
    private readonly IUiDispatcher _ui;

    /// <summary>全部台账行（非生产样品），未筛选。</summary>
    private List<SampleRowItem> _allSctRows = new();

    [ObservableProperty]
    private ObservableCollection<SampleRowItem> _sctSamples = new();

    [ObservableProperty]
    private ObservableCollection<SampleRowItem> _productionSamples = new();

    /// <summary>当前筛选键（All/SettingUp/Control/TypeStd/Production）。</summary>
    [ObservableProperty]
    private string _filterKey = FilterAll;

    /// <summary>生产样品是否存在记录（控制空态文案显隐）。</summary>
    [ObservableProperty]
    private bool _hasProduction;

    /// <summary>正在加载标记（防重复加载）。</summary>
    [ObservableProperty]
    private bool _isLoading;

    public SamplesViewModel(ISctSampleRepository samples, IUiDispatcher ui)
    {
        _samples = samples;
        _ui = ui;
    }

    /// <summary>导航进入时加载（导航路径为 fire-and-forget；测试可 await LoadCommand.ExecuteAsync 保证确定性）。</summary>
    public void OnNavigatedTo() => LoadCommand.Execute(null);

    /// <summary>从数据库加载样品并刷新两个表格（I/O 在后台线程，结果经 IUiDispatcher 回 UI）。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var rows = await Task.Run(() => _samples.GetAll().Select(MapRow).ToList());
            _ui.Invoke(() =>
            {
                _allSctRows = rows.Where(r => r.SampleTypeKey != SampleType.Production).ToList();
                var production = rows.Where(r => r.SampleTypeKey == SampleType.Production).ToList();
                HasProduction = production.Count > 0;
                ApplyFilter(FilterKey);
                ProductionSamples = new ObservableCollection<SampleRowItem>(production);
            });
        }
        finally
        {
            _ui.Invoke(() => IsLoading = false);
        }
    }

    /// <summary>切换类型筛选。</summary>
    [RelayCommand]
    private void SetFilter(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        FilterKey = key;
        ApplyFilter(key);
    }

    private void ApplyFilter(string key)
    {
        var filtered = key == FilterAll
            ? _allSctRows
            : _allSctRows.Where(r => r.SampleTypeKey == key).ToList();
        SctSamples = new ObservableCollection<SampleRowItem>(filtered);
    }

    private static SampleRowItem MapRow(SctSample s) => new()
    {
        SampleId = s.SampleId,
        SampleTypeKey = s.SampleType,
        TypeDisplay = s.SampleType switch
        {
            SampleType.SettingUp => "设置样",
            SampleType.Control => "控制样",
            SampleType.TypeStd => "类型标准化样",
            SampleType.Production => "生产样品",
            _ => s.SampleType,
        },
        AlloyType = s.AlloyType,
        RackPosition = s.RackPosition,
        ThicknessText = $"{s.CurrentThickness:0.0#} / {s.InitialThickness:0.0#}",
        MinThicknessText = $"{s.MinThickness:0.0#}",
        UsageText = $"{s.UsageCount}/{s.MaxUsageCount}",
        Status = s.Status,
        StatusText = s.Status switch
        {
            SampleStatus.Normal => "● 正常",
            SampleStatus.NeedMeasure => "▲ 需测高",
            SampleStatus.Warning => "▲ 超差预警",
            SampleStatus.Disabled => "✖ 禁用（过薄）",
            _ => s.Status,
        },
        LastHeightText = s.LastHeightCheckTime?.ToString("MM-dd HH:mm") ?? "—",
    };
}
