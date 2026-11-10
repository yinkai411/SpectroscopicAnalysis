namespace SpectroscopicAnalysis.Host.ViewModels;

/// <summary>页面生命周期：导航到页面时由 MainViewModel 调用（加载该页数据等）。</summary>
public interface IPageLifecycle
{
    /// <summary>导航进入页面时触发（数据加载等准备动作）。</summary>
    void OnNavigatedTo();
}
