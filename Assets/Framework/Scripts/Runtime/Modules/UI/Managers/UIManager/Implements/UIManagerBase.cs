/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManagerBase.cs
 * author:    taoye
 * created:   2026/3/3
 * descrip:   UI 管理器基类
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 管理器基类。
    /// </summary>
    internal abstract class UIManagerBase : FrameworkManager, IUIManager
    {
        /// <summary>
        /// 异步打开视图失败事件，参数依次为视图序列编号、资源地址和错误信息。
        /// </summary>
        public abstract event Action<int, string, string> OnOpenUIViewFail;

        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 7;

        /// <summary>
        /// 获取视图分组数量。
        /// </summary>
        public abstract int UIGroupCount { get; }

        /// <summary>
        /// 获取或设置视图实例对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        public abstract float InstanceAutoReleaseInterval { get; set; }

        /// <summary>
        /// 获取或设置视图实例对象池的容量上限。
        /// </summary>
        public abstract int InstanceCapacity { get; set; }

        /// <summary>
        /// 获取或设置视图实例对象在对象池中的过期秒数。
        /// </summary>
        public abstract float InstanceExpireTime { get; set; }

        /// <summary>
        /// 获取或设置视图实例对象池的优先级。
        /// </summary>
        public abstract int InstancePriority { get; set; }

        /// <summary>
        /// 获取或设置每帧最多销毁的 UI 数量（回收队列每帧处理上限）。
        /// </summary>
        public abstract int DestroyMaxNumPerFrame { get; set; }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(UIManagerConfig config);

        /// <summary>
        /// 创建视图实例对象池（需在 ObjectPoolManager 就绪后调用）。
        /// </summary>
        public abstract void CreateInstancePool();

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 异步加载并解析 UI 视图注册表，按 UnitSettings 并行加载各注册表文件。
        /// </summary>
        /// <returns>是否全部加载成功。</returns>
        public abstract UniTask<bool> LoadAsync();

        /// <summary>
        /// 同步加载并解析 UI 视图注册表。
        /// </summary>
        public abstract void LoadSync();

        /// <summary>
        /// 按视图类型同步打开，资源地址、分组、暂停与池化全部使用 UI 表配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewSync<T>(object userData = null) where T : UIView;

        /// <summary>
        /// 按 UI 表的 Name 同步打开，全部行为使用该行配置。
        /// </summary>
        /// <param name="uiViewName">UI 表的 Name 列值，不是 Excel 文件名或资源地址。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewSync(string uiViewName, object userData = null);

        /// <summary>
        /// 按唯一资源地址查表后同步打开；地址对应多条配置时须改用名称入口。
        /// </summary>
        /// <param name="assetLocation">UI 表的 AssetLocation 列值，按原字符串精确匹配。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewByAssetSync(string assetLocation, object userData = null);

        /// <summary>
        /// 兼容旧版泛型同步打开签名，暂停行为以 UI 表配置为准。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="pauseCoveredUIView">旧版暂停参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        [Obsolete("打开行为统一由 UI 表配置决定，开关参数不再生效；请使用不带开关参数的打开接口。")]
        public virtual int OpenUIViewSync<T>(bool pauseCoveredUIView, object userData = null) where T : UIView
        {
            return OpenUIViewSync<T>(userData);
        }

        /// <summary>
        /// 兼容旧版泛型同步打开签名，暂停与池化均以 UI 表配置为准。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="pauseCoveredUIView">旧版暂停参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="inObjectPools">旧版池化参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        [Obsolete("打开行为统一由 UI 表配置决定，开关参数不再生效；请使用不带开关参数的打开接口。")]
        public virtual int OpenUIViewSync<T>(bool pauseCoveredUIView, bool inObjectPools, object userData = null) where T : UIView
        {
            return OpenUIViewSync<T>(userData);
        }

        /// <summary>
        /// 按视图类型异步打开，资源地址、分组、暂停与池化全部使用 UI 表配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewAsync<T>(object userData = null) where T : UIView;

        /// <summary>
        /// 按 UI 表的 Name 异步打开，全部行为使用该行配置。
        /// </summary>
        /// <param name="uiViewName">UI 表的 Name 列值，不是 Excel 文件名或资源地址。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewAsync(string uiViewName, object userData = null);

        /// <summary>
        /// 按唯一资源地址查表后异步打开；地址对应多条配置时须改用名称入口。
        /// </summary>
        /// <param name="assetLocation">UI 表的 AssetLocation 列值，按原字符串精确匹配。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        public abstract int OpenUIViewByAssetAsync(string assetLocation, object userData = null);

        /// <summary>
        /// 兼容旧版泛型异步打开签名，暂停行为以 UI 表配置为准。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="pauseCoveredUIView">旧版暂停参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        [Obsolete("打开行为统一由 UI 表配置决定，开关参数不再生效；请使用不带开关参数的打开接口。")]
        public virtual int OpenUIViewAsync<T>(bool pauseCoveredUIView, object userData = null) where T : UIView
        {
            return OpenUIViewAsync<T>(userData);
        }

        /// <summary>
        /// 兼容旧版泛型异步打开签名，暂停与池化均以 UI 表配置为准。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型，其短类名必须匹配 UI 表的 Name。</typeparam>
        /// <param name="pauseCoveredUIView">旧版暂停参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="inObjectPools">旧版池化参数，保留签名供迁移；实际使用 UI 表配置。</param>
        /// <param name="userData">传递给视图的业务数据，不参与 UI 行为配置。</param>
        /// <returns>视图序列编号；未找到唯一配置时返回 -1，未创建打开请求。</returns>
        [Obsolete("打开行为统一由 UI 表配置决定，开关参数不再生效；请使用不带开关参数的打开接口。")]
        public virtual int OpenUIViewAsync<T>(bool pauseCoveredUIView, bool inObjectPools, object userData = null) where T : UIView
        {
            return OpenUIViewAsync<T>(userData);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="serialID">要关闭视图的序列编号。</param>
        public virtual void CloseUIView(int serialID)
        {
            CloseUIView(serialID, null);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="serialID">要关闭视图的序列编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public abstract void CloseUIView(int serialID, object userData);

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="uiView">要关闭的视图。</param>
        public virtual void CloseUIView(IUIView uiView)
        {
            CloseUIView(uiView, null);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="uiView">要关闭的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public abstract void CloseUIView(IUIView uiView, object userData);

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="serialIDs">要关闭视图的序列编号列表。</param>
        public virtual void CloseUIViews(int[] serialIDs)
        {
            CloseUIViews(serialIDs, null);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="serialIDs">要关闭视图的序列编号列表。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void CloseUIViews(int[] serialIDs, object userData)
        {
            if (serialIDs == null)
            {
                return;
            }

            foreach (int serialID in serialIDs)
            {
                CloseUIView(serialID, userData);
            }
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="uiViews">要关闭的视图列表。</param>
        public virtual void CloseUIViews(IUIView[] uiViews)
        {
            CloseUIViews(uiViews, null);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="uiViews">要关闭的视图列表。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void CloseUIViews(IUIView[] uiViews, object userData)
        {
            if (uiViews == null)
            {
                return;
            }

            foreach (IUIView uiView in uiViews)
            {
                CloseUIView(uiView, userData);
            }
        }

        /// <summary>
        /// 关闭所有已加载的视图。
        /// </summary>
        public virtual void CloseAllLoadedUIViews()
        {
            CloseAllLoadedUIViews(null);
        }

        /// <summary>
        /// 关闭所有已加载的视图。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public abstract void CloseAllLoadedUIViews(object userData);

        /// <summary>
        /// 关闭所有正在加载的视图。
        /// </summary>
        public abstract void CloseAllLoadingUIViews();

        /// <summary>
        /// 增加视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="uiGroupHelper">视图分组辅助器。</param>
        /// <returns>是否增加视图分组成功。</returns>
        public virtual bool AddUIGroup(string uiGroupName, IUIGroupHelper uiGroupHelper)
        {
            return AddUIGroup(uiGroupName, 0, uiGroupHelper);
        }

        /// <summary>
        /// 增加视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="uiGroupDepth">视图分组深度。</param>
        /// <param name="uiGroupHelper">视图分组辅助器。</param>
        /// <returns>是否增加视图分组成功。</returns>
        public abstract bool AddUIGroup(string uiGroupName, int uiGroupDepth, IUIGroupHelper uiGroupHelper);

        /// <summary>
        /// 获取视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>要获取的视图分组。</returns>
        public abstract IUIGroup GetUIGroup(string uiGroupName);

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <returns>所有视图分组。</returns>
        public abstract IUIGroup[] GetAllUIGroups();

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <param name="results">所有视图分组。</param>
        public abstract void GetAllUIGroups(List<IUIGroup> results);

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>要获取的视图。</returns>
        public abstract IUIView GetUIView(int serialID);

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图。</returns>
        public abstract IUIView GetUIView(string assetLocation);

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图列表。</returns>
        public abstract IUIView[] GetUIViews(string assetLocation);

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="results">要获取的视图列表。</param>
        public abstract void GetUIViews(string assetLocation, List<IUIView> results);

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <returns>所有已加载的视图。</returns>
        public abstract IUIView[] GetAllLoadedUIViews();

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <param name="results">所有已加载的视图。</param>
        public abstract void GetAllLoadedUIViews(List<IUIView> results);

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <returns>所有正在加载视图的序列编号。</returns>
        public abstract int[] GetAllLoadingUIViewSerialIDs();

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <param name="results">所有正在加载视图的序列编号。</param>
        public abstract void GetAllLoadingUIViewSerialIDs(List<int> results);

        /// <summary>
        /// 是否存在视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>是否存在视图分组。</returns>
        public abstract bool HasUIGroup(string uiGroupName);

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否存在视图。</returns>
        public abstract bool HasUIView(int serialID);

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否存在视图。</returns>
        public abstract bool HasUIView(string assetLocation);

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否正在加载视图。</returns>
        public abstract bool IsLoadingUIView(int serialID);

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否正在加载视图。</returns>
        public abstract bool IsLoadingUIView(string assetLocation);

        /// <summary>
        /// 是否是合法的视图。
        /// </summary>
        /// <param name="uiView">视图。</param>
        /// <returns>视图是否合法。</returns>
        public abstract bool IsValidUIView(IUIView uiView);

        /// <summary>
        /// 激活视图。
        /// </summary>
        /// <param name="uiView">要激活的视图。</param>
        public virtual void RefocusUIView(IUIView uiView)
        {
            RefocusUIView(uiView, null);
        }

        /// <summary>
        /// 激活视图。
        /// </summary>
        /// <param name="uiView">要激活的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public abstract void RefocusUIView(IUIView uiView, object userData);

        /// <summary>
        /// 设置视图目标对象是否被加锁。
        /// </summary>
        /// <param name="uiViewTarget">要设置是否被加锁的视图目标对象。</param>
        /// <param name="locked">视图目标对象是否被加锁。</param>
        public abstract void SetUIViewTargetLocked(object uiViewTarget, bool locked);

        /// <summary>
        /// 设置视图目标对象的优先级。
        /// </summary>
        /// <param name="uiViewTarget">要设置优先级的视图目标对象。</param>
        /// <param name="priority">视图目标对象优先级。</param>
        public abstract void SetUIViewTargetPriority(object uiViewTarget, int priority);

        /// <summary>
        /// 获取设备安全区域（实际物理像素区域信息）。
        /// </summary>
        /// <param name="isForGUISystem">是否以 GUI 系统坐标系为准。</param>
        /// <returns>矩形信息（实际物理像素区域，非画布自动缩放后的坐标）。</returns>
        public abstract Rect GetDeviceSafeArea(bool isForGUISystem = false);
    }
}
