namespace SpectroscopicAnalysis.Host.Data.Models;

/// <summary>样品状态（与任务清单状态机枚举命名一致，禁止自创状态名）。</summary>
public static class SampleStatus
{
    public const string Normal = "Normal";
    public const string NeedMeasure = "NeedMeasure";
    public const string Warning = "Warning";
    public const string Disabled = "Disabled";
}

/// <summary>样品类型（SCT 样品 + 生产样品）。</summary>
public static class SampleType
{
    public const string SettingUp = "SettingUp";
    public const string Control = "Control";
    public const string TypeStd = "TypeStd";
    public const string Production = "Production";
}

/// <summary>样品档案（表 SCT_Sample，字段见任务清单 2.1）。</summary>
public sealed class SctSample
{
    public string SampleId { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    public string SampleType { get; set; } = string.Empty;
    public string? AlloyType { get; set; }
    public string? RackPosition { get; set; }
    public double InitialThickness { get; set; }
    public double CurrentThickness { get; set; }
    public double MinThickness { get; set; }
    public int UsageCount { get; set; }
    public int MaxUsageCount { get; set; }
    public string Status { get; set; } = SampleStatus.Normal;
    public DateTime? LastUseTime { get; set; }
    public DateTime? LastHeightCheckTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedTime { get; set; }
}
