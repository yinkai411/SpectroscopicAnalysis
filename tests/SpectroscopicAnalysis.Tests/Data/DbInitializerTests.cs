using System.IO;
using Microsoft.Data.Sqlite;
using SpectroscopicAnalysis.Host.Data.Database;

namespace SpectroscopicAnalysis.Tests.Data;

/// <summary>建表脚本测试：幂等性 + 表完整性（开发计划 D3 验收项）。</summary>
public sealed class DbInitializerTests
{
    private static readonly string[] ExpectedTables =
    {
        "SCT_Sample",
        "SCT_CertifiedValue",
        "CorrectionCoefficient",
        "SCT_OperationLog",
        "ControlLimit",
    };

    [Fact]
    public void EnsureCreated_创建全部五张表()
    {
        using var db = new TestDb();
        new DbInitializer(db.Db).EnsureCreated();

        var tables = QueryTableNames(db.Db);
        foreach (var expected in ExpectedTables)
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public void EnsureCreated_重复执行幂等不抛异常()
    {
        using var db = new TestDb();
        var initializer = new DbInitializer(db.Db);
        initializer.EnsureCreated();
        initializer.EnsureCreated(); // 第二次不因表已存在而失败
    }

    [Fact]
    public void EnsureCreated_建表后外键编译检查通过()
    {
        // 外键引用完整性：pragma foreign_key_check 无行即通过
        using var db = new TestDb();
        new DbInitializer(db.Db).EnsureCreated();

        using var conn = db.Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_check;";
        using var reader = cmd.ExecuteReader();
        Assert.False(reader.Read());
    }

    private static List<string> QueryTableNames(Host.Data.Database.Db db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        var names = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }
}

/// <summary>临时文件库辅助：每个用例独立库文件，释放时清理（含 WAL/SHM）。</summary>
public sealed class TestDb : IDisposable
{
    public Host.Data.Database.Db Db { get; }

    public TestDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spectro-test-{Guid.NewGuid():N}.db");
        Db = new Host.Data.Database.Db(path);
        DbFilePath = path;
    }

    public string DbFilePath { get; }

    public void Dispose()
    {
        // 连接池会保持库文件句柄，删除前必须清空池（否则 File.Delete 抛 IO 异常）
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var f = DbFilePath + suffix;
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
    }
}
