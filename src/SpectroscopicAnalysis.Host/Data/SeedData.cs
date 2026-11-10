using Microsoft.Data.Sqlite;
using SpectroscopicAnalysis.Host.Data.Database;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;

namespace SpectroscopicAnalysis.Host.Data;

/// <summary>演示种子数据：与界面原型（docs/prototypes）中展示的样品一致；单事务、幂等，仅空库注入。</summary>
public sealed class SeedData
{
    private readonly Db _db;
    private readonly ISctSampleRepository _samples;

    public SeedData(Db db, ISctSampleRepository samples)
    {
        _db = db;
        _samples = samples;
    }

    /// <summary>样品表为空时注入演示数据（开发阶段便捷；生产环境由人工录入替代）。</summary>
    public void SeedIfEmpty()
    {
        if (_samples.GetAll().Count > 0)
        {
            return;
        }

        SeedDemo();
    }

    /// <summary>注入演示样品 / 标准值 / 控制限（INSERT OR IGNORE 幂等）。</summary>
    public void SeedDemo()
    {
        var samples = new List<SctSample>
        {
            new() { SampleId = "SUS-001", SampleName = "设置样 1", SampleType = SampleType.SettingUp, RackPosition = "A01", InitialThickness = 12.0, CurrentThickness = 9.8, MinThickness = 8.0, UsageCount = 50, MaxUsageCount = 50, Status = SampleStatus.NeedMeasure, LastUseTime = DateTime.Parse("2026-11-09 09:40"), LastHeightCheckTime = DateTime.Parse("2026-11-07 08:00") },
            new() { SampleId = "SUS-002", SampleName = "设置样 2", SampleType = SampleType.SettingUp, RackPosition = "A02", InitialThickness = 11.5, CurrentThickness = 11.4, MinThickness = 8.0, UsageCount = 18, MaxUsageCount = 50, Status = SampleStatus.Normal, LastUseTime = DateTime.Parse("2026-11-09 08:02"), LastHeightCheckTime = DateTime.Parse("2026-11-08 08:00") },
            new() { SampleId = "SUS-003", SampleName = "设置样 3", SampleType = SampleType.SettingUp, RackPosition = "A03", InitialThickness = 10.0, CurrentThickness = 7.2, MinThickness = 8.0, UsageCount = 33, MaxUsageCount = 50, Status = SampleStatus.Disabled, LastUseTime = DateTime.Parse("2026-11-05 10:12"), LastHeightCheckTime = DateTime.Parse("2026-11-05 14:20") },
            new() { SampleId = "CTL-001", SampleName = "控制样（低）", SampleType = SampleType.Control, AlloyType = "1xxx", RackPosition = "B01", InitialThickness = 17.0, CurrentThickness = 16.9, MinThickness = 6.0, UsageCount = 22, MaxUsageCount = 60, Status = SampleStatus.Normal, LastHeightCheckTime = DateTime.Parse("2026-11-06 08:00") },
            new() { SampleId = "CTL-002", SampleName = "控制样（中）", SampleType = SampleType.Control, AlloyType = "6xxx", RackPosition = "B02", InitialThickness = 19.0, CurrentThickness = 18.6, MinThickness = 6.0, UsageCount = 41, MaxUsageCount = 60, Status = SampleStatus.Normal, LastHeightCheckTime = DateTime.Parse("2026-11-08 08:00") },
            new() { SampleId = "CTL-003", SampleName = "控制样（高）", SampleType = SampleType.Control, AlloyType = "5xxx", RackPosition = "B03", InitialThickness = 18.0, CurrentThickness = 17.8, MinThickness = 6.0, UsageCount = 9, MaxUsageCount = 60, Status = SampleStatus.Normal, LastHeightCheckTime = DateTime.Parse("2026-11-08 08:00") },
            new() { SampleId = "CTL-004", SampleName = "控制样（低）", SampleType = SampleType.Control, AlloyType = "3xxx", RackPosition = "B04", InitialThickness = 17.5, CurrentThickness = 17.5, MinThickness = 6.0, UsageCount = 0, MaxUsageCount = 60, Status = SampleStatus.Normal },
            new() { SampleId = "TS-6061-01", SampleName = "类型标准化样 6061", SampleType = SampleType.TypeStd, AlloyType = "6061", RackPosition = "C01", InitialThickness = 16.0, CurrentThickness = 15.2, MinThickness = 7.0, UsageCount = 12, MaxUsageCount = 40, Status = SampleStatus.Normal, LastHeightCheckTime = DateTime.Parse("2026-11-01 09:30") },
            new() { SampleId = "TS-1050-01", SampleName = "类型标准化样 1050", SampleType = SampleType.TypeStd, AlloyType = "1050", RackPosition = "C02", InitialThickness = 15.0, CurrentThickness = 14.7, MinThickness = 7.0, UsageCount = 28, MaxUsageCount = 40, Status = SampleStatus.Normal, LastHeightCheckTime = DateTime.Parse("2026-10-30 16:00") },
        };

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        foreach (var s in samples)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO SCT_Sample (SampleID, SampleName, SampleType, AlloyType, RackPosition,
                    InitialThickness, CurrentThickness, MinThickness, UsageCount, MaxUsageCount,
                    Status, LastUseTime, LastHeightCheckTime, IsActive)
                VALUES ($id, $name, $type, $alloy, $rack, $initThk, $curThk, $minThk, $usage, $maxUsage,
                    $status, $lastUse, $lastHeight, 1)
                """;
            cmd.Parameters.AddWithValue("$id", s.SampleId);
            cmd.Parameters.AddWithValue("$name", s.SampleName);
            cmd.Parameters.AddWithValue("$type", s.SampleType);
            cmd.Parameters.AddWithValue("$alloy", (object?)s.AlloyType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$rack", (object?)s.RackPosition ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$initThk", s.InitialThickness);
            cmd.Parameters.AddWithValue("$curThk", s.CurrentThickness);
            cmd.Parameters.AddWithValue("$minThk", s.MinThickness);
            cmd.Parameters.AddWithValue("$usage", s.UsageCount);
            cmd.Parameters.AddWithValue("$maxUsage", s.MaxUsageCount);
            cmd.Parameters.AddWithValue("$status", s.Status);
            cmd.Parameters.AddWithValue("$lastUse", (object?)s.LastUseTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lastHeight", (object?)s.LastHeightCheckTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        // SUS-002 标准值（演示）
        InsertCertifiedValue(conn, tx, "SUS-002", "Si", 0.0812, 0.0008);
        InsertCertifiedValue(conn, tx, "SUS-002", "Fe", 0.2045, 0.0015);
        InsertCertifiedValue(conn, tx, "SUS-002", "Cu", 0.0018, 0.0002);

        // CTL-002 控制限（±2σ 示例）
        InsertControlLimit(conn, tx, "CTL-002", "Si", 0.90, 0.70, "Sigma");
        InsertControlLimit(conn, tx, "CTL-002", "Fe", 0.28, 0.14, "Sigma");
        InsertControlLimit(conn, tx, "CTL-002", "Mg", 1.05, 0.85, "Sigma");

        tx.Commit();
    }

    private static void InsertCertifiedValue(SqliteConnection conn, SqliteTransaction tx, string sampleId, string element, double value, double uncertainty)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO SCT_CertifiedValue (SampleID, Element, CertifiedValue, Unit, Uncertainty)
            VALUES ($sid, $element, $value, '%', $unc)
            """;
        cmd.Parameters.AddWithValue("$sid", sampleId);
        cmd.Parameters.AddWithValue("$element", element);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$unc", uncertainty);
        cmd.ExecuteNonQuery();
    }

    private static void InsertControlLimit(SqliteConnection conn, SqliteTransaction tx, string sampleId, string element, double upper, double lower, string limitType)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR IGNORE INTO ControlLimit (SampleID, Element, UpperLimit, LowerLimit, LimitType)
            VALUES ($sid, $element, $upper, $lower, $type)
            """;
        cmd.Parameters.AddWithValue("$sid", sampleId);
        cmd.Parameters.AddWithValue("$element", element);
        cmd.Parameters.AddWithValue("$upper", upper);
        cmd.Parameters.AddWithValue("$lower", lower);
        cmd.Parameters.AddWithValue("$type", limitType);
        cmd.ExecuteNonQuery();
    }
}
