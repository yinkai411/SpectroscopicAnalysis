using CommunityToolkit.Mvvm.ComponentModel;
using SpectroscopicAnalysis.Host.Data.Models;
using SpectroscopicAnalysis.Host.Data.Repositories;
using SpectroscopicAnalysis.Host.Ui;
using SpectroscopicAnalysis.Host.ViewModels;

namespace SpectroscopicAnalysis.Tests;

/// <summary>Fake 仓储：返回预设列表（不做真实 I/O）。供多个 ViewModel 测试类共用。</summary>
internal sealed class FakeSampleRepository : ISctSampleRepository
{
    public List<SctSample> Store { get; } = new();

    public SctSample? GetById(string sampleId) => Store.FirstOrDefault(s => s.SampleId == sampleId);

    public IReadOnlyList<SctSample> GetAll() => Store.ToList();

    public void Insert(SctSample sample) => Store.Add(sample);

    public void Update(SctSample sample) { }

    public void Delete(string sampleId) { }

    public void UpdateAfterSpark(string sampleId, double currentThickness, string status, DateTime usedAt) { }
}

/// <summary>Fake 调度器：同步执行（保证测试确定性）。</summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action) => action();

    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
}

/// <summary>样品档案 ViewModel 测试：数据加载 / 展示映射 / 类型筛选（开发计划 D4 先行项验收）。</summary>
public sealed class SamplesViewModelTests
{

    private static SctSample Make(string id, string type, string status = SampleStatus.Normal) => new()
    {
        SampleId = id,
        SampleName = id,
        SampleType = type,
        AlloyType = "6xxx",
        RackPosition = "A01",
        InitialThickness = 12.0,
        CurrentThickness = 9.8,
        MinThickness = 8.0,
        UsageCount = 50,
        MaxUsageCount = 50,
        Status = status,
        LastHeightCheckTime = new DateTime(2026, 11, 7, 8, 0, 0),
    };

    private static SamplesViewModel CreateWith(params SctSample[] samples)
    {
        var repo = new FakeSampleRepository();
        foreach (var s in samples)
        {
            repo.Store.Add(s);
        }
        return new SamplesViewModel(repo, new FakeUiDispatcher());
    }

    [Fact]
    public async Task Load_台账含非生产样品_生产样品独立()
    {
        var vm = CreateWith(
            Make("SUS-001", SampleType.SettingUp),
            Make("CTL-002", SampleType.Control, SampleStatus.NeedMeasure),
            Make("TS-6061-01", SampleType.TypeStd),
            Make("P-20261110-07", SampleType.Production));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.SctSamples.Count);                    // 非生产样品 3 条
        Assert.Single(vm.ProductionSamples);                     // 生产样品 1 条
        Assert.True(vm.HasProduction);
    }

    [Fact]
    public async Task Load_展示映射正确()
    {
        var vm = CreateWith(
            Make("SUS-001", SampleType.SettingUp),
            Make("SUS-003", SampleType.SettingUp, SampleStatus.Disabled),
            Make("TS-6061-01", SampleType.TypeStd));

        await vm.LoadCommand.ExecuteAsync(null);
        var sus = vm.SctSamples.First(r => r.SampleId == "SUS-001");

        Assert.Equal("设置样", sus.TypeDisplay);
        Assert.Equal("9.8 / 12.0", sus.ThicknessText);
        Assert.Equal("50/50", sus.UsageText);
        Assert.Equal("11-07 08:00", sus.LastHeightText);
        Assert.Equal("● 正常", sus.StatusText);

        var disabled = vm.SctSamples.First(r => r.SampleId == "SUS-003");
        Assert.Equal("类型标准化样", vm.SctSamples.First(r => r.SampleId == "TS-6061-01").TypeDisplay);
        Assert.Equal("✖ 禁用（过薄）", disabled.StatusText);
    }

    [Fact]
    public async Task SetFilter_按类型过滤()
    {
        var vm = CreateWith(
            Make("SUS-001", SampleType.SettingUp),
            Make("CTL-002", SampleType.Control),
            Make("TS-6061-01", SampleType.TypeStd));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SetFilterCommand.Execute("Control");
        var filtered = vm.SctSamples.ToList();
        Assert.Single(filtered);
        Assert.Equal("CTL-002", filtered[0].SampleId);

        vm.SetFilterCommand.Execute(SamplesViewModel.FilterAll);
        Assert.Equal(3, vm.SctSamples.Count);
    }

    [Fact]
    public async Task Load_空库时两张表为空且无生产样品()
    {
        var vm = CreateWith();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.SctSamples);
        Assert.Empty(vm.ProductionSamples);
        Assert.False(vm.HasProduction);
    }

    [Fact]
    public async Task Load_重复调用幂等()
    {
        var vm = CreateWith(Make("SUS-001", SampleType.SettingUp));
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.SctSamples);
    }
}
