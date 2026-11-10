using SpectroscopicAnalysis.Host.Data.Models;

namespace SpectroscopicAnalysis.Host.Data.Repositories;

/// <summary>SCT 样品档案仓储（其余 4 表仓储随 D4 按同一模式补齐）。</summary>
public interface ISctSampleRepository
{
    SctSample? GetById(string sampleId);

    IReadOnlyList<SctSample> GetAll();

    void Insert(SctSample sample);

    void Update(SctSample sample);

    void Delete(string sampleId);

    /// <summary>激发完成后持久化使用结果：次数 +1、当前厚度、状态、上次使用时间（事务内更新）。</summary>
    void UpdateAfterSpark(string sampleId, double currentThickness, string status, DateTime usedAt);
}
