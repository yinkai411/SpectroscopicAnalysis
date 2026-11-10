using Microsoft.Data.Sqlite;
using SpectroscopicAnalysis.Host.Data.Database;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;
using SpectroscopicAnalysis.Tests.Data;

namespace SpectroscopicAnalysis.Tests.Data;

/// <summary>样品仓储测试：CRUD / 事务更新 / 外键约束（开发计划 D3 验收项）。</summary>
public sealed class SctSampleRepositoryTests
{
    /// <summary>测试上下文：独立临时库 + 已建表 + 仓储；释放时清理库文件。</summary>
    private sealed class TestContext : IDisposable
    {
        public TestDb Db { get; }
        public SctSampleRepository Repo { get; }

        public TestContext()
        {
            Db = new TestDb();
            new DbInitializer(Db.Db).EnsureCreated();
            Repo = new SctSampleRepository(Db.Db);
        }

        public void Dispose() => Db.Dispose();
    }

    private static TestContext Create() => new();

    private static SctSample MakeSample(string id = "SUS-001") => new()
    {
        SampleId = id,
        SampleName = "设置样 1",
        SampleType = SampleType.SettingUp,
        AlloyType = null,
        RackPosition = "A01",
        InitialThickness = 12.0,
        CurrentThickness = 9.8,
        MinThickness = 8.0,
        UsageCount = 49,
        MaxUsageCount = 50,
        Status = SampleStatus.Normal,
        LastUseTime = new DateTime(2026, 11, 9, 9, 40, 0),
        LastHeightCheckTime = new DateTime(2026, 11, 7, 8, 0, 0),
        IsActive = true,
        CreatedTime = new DateTime(2026, 10, 1, 8, 0, 0),
    };

    [Fact]
    public void Insert_GetById_往返字段一致()
    {
        using var ctx = Create();
        var expected = MakeSample();
        ctx.Repo.Insert(expected);

        var actual = ctx.Repo.GetById("SUS-001");
        Assert.NotNull(actual);
        Assert.Equal(expected.SampleId, actual.SampleId);
        Assert.Equal(expected.SampleName, actual.SampleName);
        Assert.Equal(expected.SampleType, actual.SampleType);
        Assert.Equal(expected.RackPosition, actual.RackPosition);
        Assert.Equal(expected.InitialThickness, actual.InitialThickness);
        Assert.Equal(expected.CurrentThickness, actual.CurrentThickness);
        Assert.Equal(expected.UsageCount, actual.UsageCount);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.LastUseTime, actual.LastUseTime);
        Assert.True(actual.IsActive);
    }

    [Fact]
    public void GetById_不存在返回Null()
    {
        using var ctx = Create();
        Assert.Null(ctx.Repo.GetById("NO-SUCH"));
    }

    [Fact]
    public void UpdateAfterSpark_次数加一且厚度状态时间更新()
    {
        using var ctx = Create();
        ctx.Repo.Insert(MakeSample());

        var usedAt = new DateTime(2026, 11, 10, 10, 0, 0);
        ctx.Repo.UpdateAfterSpark("SUS-001", currentThickness: 9.5, status: SampleStatus.NeedMeasure, usedAt: usedAt);

        var after = ctx.Repo.GetById("SUS-001");
        Assert.NotNull(after);
        Assert.Equal(50, after.UsageCount);              // 49 + 1
        Assert.Equal(9.5, after.CurrentThickness);
        Assert.Equal(SampleStatus.NeedMeasure, after.Status);
        Assert.Equal(usedAt, after.LastUseTime);
    }

    [Fact]
    public void UpdateAfterSpark_样品不存在抛出异常()
    {
        using var ctx = Create();
        Assert.Throws<InvalidOperationException>(() =>
            ctx.Repo.UpdateAfterSpark("NO-SUCH", 1.0, SampleStatus.Normal, DateTime.Now));
    }

    [Fact]
    public void GetAll_Delete_正常工作()
    {
        using var ctx = Create();
        ctx.Repo.Insert(MakeSample("SUS-001"));
        ctx.Repo.Insert(MakeSample("SUS-002"));
        Assert.Equal(2, ctx.Repo.GetAll().Count);

        ctx.Repo.Delete("SUS-001");
        Assert.Single(ctx.Repo.GetAll());
        Assert.Null(ctx.Repo.GetById("SUS-001"));
    }

    [Fact]
    public void 外键约束_对不存在样品插标准值抛异常()
    {
        using var ctx = Create();
        using var conn = ctx.Db.Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SCT_CertifiedValue (SampleID, Element, CertifiedValue, Unit)
            VALUES ('NO-SUCH', 'Si', 0.08, '%')
            """;
        Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
    }
}
