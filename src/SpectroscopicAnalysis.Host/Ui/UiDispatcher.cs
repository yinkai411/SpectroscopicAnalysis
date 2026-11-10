using System.Windows.Threading;

namespace SpectroscopicAnalysis.Host.Ui;

/// <summary>基于 WPF Dispatcher 的 UI 线程调度实现（UI 层组件，业务层不得引用）。</summary>
public sealed class UiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;
}
