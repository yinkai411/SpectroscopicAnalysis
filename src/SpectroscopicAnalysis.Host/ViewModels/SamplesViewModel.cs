using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;
using SpectroscopicAnalysis.Host.Ui;

namespace SpectroscopicAnalysis.Host.ViewModels;

/// <summary>
/// 台账行展示模型：领域数据 → 展示文本映射（开发规范 3.3：VM 负责映射为展示模型）。
/// SampleTypeKey 为类型键（筛选用，非绑定字段）；Status 为原始枚举串（视图 DataTrigger 切换徽章颜色）；
/// IsConfirmDelete 为删除两步确认态（视图 DataTrigger 切换按钮样式）。
/// </summary>
public sealed partial class SampleRowItem : ObservableObject
{
    [ObservableProperty]
    private bool _isConfirmDelete;

    public required string SampleId { get; init; }
    public required string SampleName { get; init; }
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

    /// <summary>删除失败提示（空即无错误；如数据库约束冲突）。</summary>
    [ObservableProperty]
    private string? _deleteError;

    // ==================== 样品注册（任务清单"入库初始化"） ====================

    /// <summary>样品类型下拉选项（展示文本：设置样/控制样/类型标准化样/生产样品）。</summary>
    public static readonly IReadOnlyList<string> SampleTypeOptions = new[]
    {
        "设置样", "控制样", "类型标准化样", "生产样品",
    };

    [ObservableProperty]
    private bool _isRegisterOpen;

    [ObservableProperty]
    private string _regSampleId = string.Empty;

    [ObservableProperty]
    private string _regSampleName = string.Empty;

    [ObservableProperty]
    private string _regSampleType = SampleTypeOptions[0];

    [ObservableProperty]
    private string? _regAlloyType;

    [ObservableProperty]
    private string? _regRackPosition;

    [ObservableProperty]
    private string _regInitialThickness = string.Empty;

    [ObservableProperty]
    private string _regMinThickness = string.Empty;

    [ObservableProperty]
    private string _regMaxUsageCount = string.Empty;

    /// <summary>注册校验错误提示（空即无错误）。</summary>
    [ObservableProperty]
    private string? _registerError;

    /// <summary>注册提交中标记（防重复提交）。</summary>
    [ObservableProperty]
    private bool _isRegistering;

    /// <summary>切换注册表单显隐。</summary>
    [RelayCommand]
    private void ToggleRegister() => IsRegisterOpen = !IsRegisterOpen;

    /// <summary>取消注册：清空表单与错误并关闭面板。</summary>
    [RelayCommand]
    private void CancelRegister()
    {
        ClearRegisterForm();
        IsRegisterOpen = false;
    }

    /// <summary>提交注册：校验 → 入库初始化（CurrentThickness=Initial、UsageCount=0、Status=Normal）→ 刷新。</summary>
    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsRegistering)
        {
            return;
        }

        var error = ValidateRegisterForm();
        if (error is not null)
        {
            RegisterError = error;
            return;
        }

        var sample = BuildSampleFromForm();
        IsRegistering = true;
        RegisterError = null;
        try
        {
            var duplicate = await Task.Run(() => _samples.GetById(sample.SampleId) is not null);
            if (duplicate)
            {
                RegisterError = $"样品编号已存在：{sample.SampleId}";
                return;
            }

            await Task.Run(() => _samples.Insert(sample));
            _ui.Invoke(() =>
            {
                ClearRegisterForm();
                IsRegisterOpen = false;
            });
            // 入库后刷新列表（LoadAsync 内部有 IsLoading 防抖，此处直接复用）
            LoadCommand.Execute(null);
        }
        finally
        {
            _ui.Invoke(() => IsRegistering = false);
        }
    }

    /// <summary>表单校验；返回错误消息，null 表示通过。</summary>
    private string? ValidateRegisterForm()
    {
        if (string.IsNullOrWhiteSpace(RegSampleId))
        {
            return "样品编号不能为空";
        }
        if (string.IsNullOrWhiteSpace(RegSampleName))
        {
            return "样品名称不能为空";
        }
        if (string.IsNullOrWhiteSpace(RegRackPosition))
        {
            return "样品架位置不能为空";
        }
        if (!double.TryParse(RegInitialThickness, out var init) || init <= 0)
        {
            return "初始厚度必须为大于 0 的数值";
        }
        if (!double.TryParse(RegMinThickness, out var min) || min <= 0)
        {
            return "最小厚度必须为大于 0 的数值";
        }
        if (!int.TryParse(RegMaxUsageCount, out var maxUsage) || maxUsage < 1)
        {
            return "最大使用次数必须为不小于 1 的整数";
        }
        return null;
    }

    private SctSample BuildSampleFromForm() => new()
    {
        SampleId = RegSampleId.Trim(),
        SampleName = RegSampleName.Trim(),
        SampleType = TypeDisplayToKey(RegSampleType),
        AlloyType = string.IsNullOrWhiteSpace(RegAlloyType) ? null : RegAlloyType.Trim(),
        RackPosition = RegRackPosition?.Trim(), // 校验已确保非空
        InitialThickness = double.Parse(RegInitialThickness),
        CurrentThickness = double.Parse(RegInitialThickness), // 入库初始化：当前厚度 = 初始厚度
        MinThickness = double.Parse(RegMinThickness),
        UsageCount = 0,
        MaxUsageCount = int.Parse(RegMaxUsageCount),
        Status = SampleStatus.Normal,
        IsActive = true,
        CreatedTime = DateTime.Now,
    };

    private void ClearRegisterForm()
    {
        RegSampleId = string.Empty;
        RegSampleName = string.Empty;
        RegSampleType = SampleTypeOptions[0];
        RegAlloyType = null;
        RegRackPosition = null;
        RegInitialThickness = string.Empty;
        RegMinThickness = string.Empty;
        RegMaxUsageCount = string.Empty;
        RegisterError = null;
    }

    /// <summary>展示文本 → 类型键（与 SampleRowItem 映射一致）。</summary>
    private static string TypeDisplayToKey(string display) => display switch
    {
        "设置样" => SampleType.SettingUp,
        "控制样" => SampleType.Control,
        "类型标准化样" => SampleType.TypeStd,
        "生产样品" => SampleType.Production,
        _ => display,
    };

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

    // ==================== 样品删除（两步确认，危险操作） ====================

    /// <summary>第一步：标记待删除行（同时复位其他行的确认态，保证单行待确认）。</summary>
    [RelayCommand]
    private void RequestDelete(SampleRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        DeleteError = null;
        foreach (var r in _allSctRows)
        {
            r.IsConfirmDelete = ReferenceEquals(r, row) && !r.IsConfirmDelete;
        }
    }

    /// <summary>第二步：确认删除——事务级联删除（样品+标准值+控制限；操作日志保留）后刷新集合。</summary>
    [RelayCommand]
    private async Task ConfirmDeleteAsync(SampleRowItem? row)
    {
        if (row is null || IsLoading)
        {
            return;
        }

        DeleteError = null;
        var snapshot = row;
        try
        {
            await Task.Run(() => _samples.DeleteWithChildren(snapshot.SampleId));
            _ui.Invoke(() =>
            {
                _allSctRows = _allSctRows.Where(r => !ReferenceEquals(r, snapshot)).ToList();
                ProductionSamples = new ObservableCollection<SampleRowItem>(
                    ProductionSamples.Where(r => !ReferenceEquals(r, snapshot)));
                ApplyFilter(FilterKey);
                HasProduction = ProductionSamples.Count > 0;
            });
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            DeleteError = $"删除失败：{ex.Message}";
        }
    }

    private static SampleRowItem MapRow(SctSample s) => new()
    {
        SampleId = s.SampleId,
        SampleName = s.SampleName,
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
