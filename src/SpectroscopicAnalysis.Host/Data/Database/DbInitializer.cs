namespace SpectroscopicAnalysis.Host.Data.Database;

/// <summary>
/// 建表脚本：字段定义见 docs/上位机任务清单.md 第三节（P19–21）与进阶设计 1.3 节。
/// 全部幂等（IF NOT EXISTS），重复执行安全。
/// </summary>
public sealed class DbInitializer
{
    private readonly Db _db;

    public DbInitializer(Db db) => _db = db;

    private static readonly string[] Scripts =
    {
        // 样品档案表
        """
        CREATE TABLE IF NOT EXISTS SCT_Sample (
            SampleID            TEXT PRIMARY KEY,
            SampleName          TEXT NOT NULL,
            SampleType          TEXT NOT NULL CHECK (SampleType IN ('SettingUp','Control','TypeStd','Production')),
            AlloyType           TEXT,
            RackPosition        TEXT,
            InitialThickness    REAL NOT NULL,
            CurrentThickness    REAL NOT NULL,
            MinThickness        REAL NOT NULL,
            UsageCount          INTEGER NOT NULL DEFAULT 0,
            MaxUsageCount       INTEGER NOT NULL,
            Status              TEXT NOT NULL CHECK (Status IN ('Normal','NeedMeasure','Warning','Disabled')),
            LastUseTime         TEXT,
            LastHeightCheckTime TEXT,
            IsActive            INTEGER NOT NULL DEFAULT 1,
            CreatedTime         TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );
        """,
        // 标准值表
        """
        CREATE TABLE IF NOT EXISTS SCT_CertifiedValue (
            ID             INTEGER PRIMARY KEY AUTOINCREMENT,
            SampleID       TEXT NOT NULL REFERENCES SCT_Sample(SampleID),
            Element        TEXT NOT NULL,
            CertifiedValue REAL NOT NULL,
            Unit           TEXT NOT NULL DEFAULT '%',
            Uncertainty    REAL
        );
        """,
        // 校正系数表（版本化：IsCurrent + Version，旧版本归档不删除；不含操作者字段——身份内容已移除）
        """
        CREATE TABLE IF NOT EXISTS CorrectionCoefficient (
            CoeffID          INTEGER PRIMARY KEY AUTOINCREMENT,
            MethodID         TEXT NOT NULL,
            Element          TEXT NOT NULL,
            Slope            REAL NOT NULL,
            Intercept        REAL NOT NULL,
            EffectiveTime    TEXT NOT NULL,
            SourceSampleID   TEXT,
            IsCurrent        INTEGER NOT NULL DEFAULT 0,
            ReferenceElement TEXT,
            Version          INTEGER NOT NULL DEFAULT 1
        );
        """,
        // SCT 操作日志表（审计全字段，见任务清单 2.4）
        """
        CREATE TABLE IF NOT EXISTS SCT_OperationLog (
            LogID            INTEGER PRIMARY KEY AUTOINCREMENT,
            SampleID         TEXT,
            OperationType    TEXT NOT NULL,
            TriggerReason    TEXT,
            RawIntensity     TEXT,
            MeasuredConc     TEXT,
            CorrectedConc    TEXT,
            IsPass           INTEGER,
            ThicknessBefore  REAL,
            ThicknessAfter   REAL,
            CoeffBefore      TEXT,
            CoeffAfter       TEXT,
            OperationTime    TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            ResultStatus     TEXT NOT NULL DEFAULT 'Success' CHECK (ResultStatus IN ('Success','Fail','Warning'))
        );
        """,
        // 控制限表
        """
        CREATE TABLE IF NOT EXISTS ControlLimit (
            LimitID    INTEGER PRIMARY KEY AUTOINCREMENT,
            SampleID   TEXT NOT NULL REFERENCES SCT_Sample(SampleID),
            Element    TEXT NOT NULL,
            UpperLimit REAL NOT NULL,
            LowerLimit REAL NOT NULL,
            LimitType  TEXT NOT NULL DEFAULT 'Sigma' CHECK (LimitType IN ('Absolute','Percent','Sigma'))
        );
        """,
        // 常用查询索引
        "CREATE INDEX IF NOT EXISTS IX_OperationLog_Sample_Time ON SCT_OperationLog(SampleID, OperationTime);",
        "CREATE INDEX IF NOT EXISTS IX_Coefficient_Method_Current ON CorrectionCoefficient(MethodID, Element, IsCurrent);",
    };

    /// <summary>创建全部表与索引（单事务执行）。</summary>
    public void EnsureCreated()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        foreach (var sql in Scripts)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
