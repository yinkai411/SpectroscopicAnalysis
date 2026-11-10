using SpectroscopicAnalysis.Host.Data.Database;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;
using SpectroscopicAnalysis.Tests.Data;

namespace SpectroscopicAnalysis.Tests.Data;

/// <summary>级联删除测试：样品 + 标准值 + 控制限同删，操作日志保留（开发规范第 5 节审计要求）。</summary>
public sealed class RepositoryDeleteTests
{
    private const string SeedSampleId = "CTL-TEST";

    private static void Exec(Host.Data.Database.Db db, string sql)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long Count(Host.Data.Database.Db db, string table, string sampleId)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE SampleID = $id";
        cmd.Parameters.AddWithValue("$id", sampleId);
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    [Fact]
    public void DeleteWithChildren_级联删除子数据并保留操作日志()
    {
        using var db = new TestDb();
        var repo = new SctSampleRepository(db.Db);
        new DbInitializer(db.Db).EnsureCreated();

        // 播种：样品 + 标准值 + 控制限 + 操作日志各一条
        Exec(db.Db, $"INSERT INTO SCT_Sample (SampleID, SampleName, SampleType, InitialThickness, CurrentThickness, MinThickness, MaxUsageCount, Status) VALUES ('{SeedSampleId}', '删除测试样品', 'Control', 10.0, 10.0, 6.0, 60, 'Normal')");
        Exec(db.Db, $"INSERT INTO SCT_CertifiedValue (SampleID, Element, CertifiedValue) VALUES ('{SeedSampleId}', 'Si', 0.08)");
        Exec(db.Db, $"INSERT INTO ControlLimit (SampleID, Element, UpperLimit, LowerLimit) VALUES ('{SeedSampleId}', 'Si', 0.10, 0.06)");
        Exec(db.Db, $"INSERT INTO SCT_OperationLog (SampleID, OperationType, ResultStatus) VALUES ('{SeedSampleId}', 'ControlCheck', 'Success')");
        Assert.Equal(1, Count(db.Db, "SCT_Sample", SeedSampleId)); // 种子就位

        repo.DeleteWithChildren(SeedSampleId);

        Assert.Equal(0, Count(db.Db, "SCT_Sample", SeedSampleId));
        Assert.Equal(0, Count(db.Db, "SCT_CertifiedValue", SeedSampleId));
        Assert.Equal(0, Count(db.Db, "ControlLimit", SeedSampleId));
        Assert.Equal(1, Count(db.Db, "SCT_OperationLog", SeedSampleId)); // 审计保留
    }

    [Fact]
    public void DeleteWithChildren_样品不存在_静默成功()
    {
        using var db = new TestDb();
        var repo = new SctSampleRepository(db.Db);
        new DbInitializer(db.Db).EnsureCreated();

        repo.DeleteWithChildren("NO-SUCH"); // 不抛异常
    }
}
