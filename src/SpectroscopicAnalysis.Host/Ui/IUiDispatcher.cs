namespace SpectroscopicAnalysis.Host.Ui;

/// <summary>
/// UI 线程调度抽象（开发规范 3.3：ViewModel 禁止直接持有 Dispatcher/操作视觉树，跨线程统一经此接口）。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>在 UI 线程执行指定操作（若已在 UI 线程则同步执行）。</summary>
    void Invoke(Action action);

    /// <summary>在 UI 线程异步执行指定操作。</summary>
    Task InvokeAsync(Action action);
}
