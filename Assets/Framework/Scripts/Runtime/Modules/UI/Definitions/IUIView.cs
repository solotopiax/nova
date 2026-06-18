/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUIView.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图接口。
    /// </summary>
    public interface IUIView
    {
        /// <summary>
        /// 获取视图资源地址（格式由业务层约定，框架侧透传）。
        /// </summary>
        string AssetLocation { get; }

        /// <summary>
        /// 获取或设置视图名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取视图序列编号。
        /// </summary>
        int SerialID { get; }

        /// <summary>
        /// 获取视图所属的视图分组。
        /// </summary>
        IUIGroup UIGroup { get; }

        /// <summary>
        /// 获取视图在视图分组中的深度。
        /// </summary>
        int DepthInUIGroup { get; }

        /// <summary>
        /// 获取是否暂停被覆盖的视图。
        /// 一般仅在全屏独占界面中使用 true，其他情况使用 false。
        /// </summary>
        bool PauseCoveredUIView { get; }

        /// <summary>
        /// 获取是否启用对象池缓存。
        /// true 表示关闭后回收到对象池等待复用，false 表示关闭后直接销毁。
        /// </summary>
        bool InObjectPools { get; }

        /// <summary>
        /// 获取视图实例。
        /// </summary>
        object Target { get; }

        /// <summary>
        /// 初始化视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroup">视图所属的视图分组。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnInit(int serialID, string assetLocation, IUIGroup uiGroup, bool pauseCoveredUIView, bool inObjectPools, bool isNewInstance, object userData);

        /// <summary>
        /// 视图回收。
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 视图打开。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnOpen(object userData);

        /// <summary>
        /// 视图关闭。
        /// </summary>
        /// <param name="isShutdown">是否是关闭视图管理器时触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnClose(bool isShutdown, object userData);

        /// <summary>
        /// 视图暂停。
        /// </summary>
        void OnPause();

        /// <summary>
        /// 视图暂停恢复。
        /// </summary>
        void OnResume();

        /// <summary>
        /// 视图遮挡。
        /// </summary>
        void OnCover();

        /// <summary>
        /// 视图遮挡恢复。
        /// </summary>
        void OnReveal();

        /// <summary>
        /// 视图激活。
        /// 激活某个视图时，也是先 Remove 再 AddFirst。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnRefocus(object userData);

        /// <summary>
        /// 视图轮询。
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// 视图深度改变。
        /// </summary>
        /// <param name="uiGroupDepth">视图分组深度。</param>
        /// <param name="depthInUIGroup">视图在视图分组中的深度。</param>
        void OnDepthChanged(int uiGroupDepth, int depthInUIGroup);
    }
}
