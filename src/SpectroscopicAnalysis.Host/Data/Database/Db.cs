using System.IO;
using Microsoft.Data.Sqlite;

namespace SpectroscopicAnalysis.Host.Data.Database;

/// <summary>
/// SQLite 连接工厂：集中管理连接串与 PRAGMA。
/// 数据库文件路径来自配置（appsettings.json → Database:Path），基于应用基目录解析；WAL 模式 + 外键强制。
/// </summary>
public sealed class Db
{
    private readonly string _connectionString;

    public Db(string databasePath)
    {
        // 确保目录存在，避免首次启动建库失败
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    /// <summary>打开连接并启用 WAL / 外键强制（PRAGMA 为连接级设置，每次打开需执行）。</summary>
    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }
}
