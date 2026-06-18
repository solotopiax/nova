/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 对外公开接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 管理器。
    /// </summary>
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 初始化 UI 管理器的新实例。
        /// </summary>
        public UIManager()
        {
            m_UIViewIndex = new Dictionary<int, IUIView>();
            m_UIGroups = new Dictionary<string, UIGroup>(StringComparer.Ordinal);
            m_UIViewsBeingLoaded = new Dictionary<int, string>();
            m_UIViewsToReleaseOnLoad = new HashSet<int>();
            m_RecycleQueue = new Queue<IUIView>();
            m_ObjectPoolManager = null;
            m_InstancePool = null;
            m_Serial = 0;
            m_IsShutdown = false;
            m_CachedUIViewResults = new List<IUIView>();
            m_CachedUIGroupResults = new List<IUIGroup>();
            m_CachedSerialIDResults = new List<int>();
            m_UIViewRegistry = new Dictionary<string, IUIViewRow>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 初始化 UI 管理器（仅存储配置，不创建实例池）。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(UIManagerConfig config)
        {
            m_InstanceAutoReleaseInterval = config.InstanceAutoReleaseInterval;
            m_InstanceCapacity = config.InstanceCapacity;
            m_InstanceExpireTime = config.InstanceExpireTime;
            m_InstancePriority = config.InstancePriority;
            m_DestroyMaxNumPerFrame = config.DestroyMaxNumPerFrame;
            m_GroupDepthFactor = config.GroupDepthFactor;
            m_ViewDepthFactor = config.ViewDepthFactor;
            m_UnitSettings = config.UnitSettings ?? new List<UIUnitSetting>();
            m_SafeAreaProvider = config.SafeAreaProvider ?? new DefaultSafeAreaProvider();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            m_PrefabManager = FrameworkManagersGroup.GetManager<IPrefabManager>();
        }

        /// <summary>
        /// 创建视图实例对象池（需在 ObjectPoolManager 就绪后调用）。
        /// </summary>
        public override void CreateInstancePool()
        {
            m_ObjectPoolManager = FrameworkManagersGroup.GetManager<IObjectPoolManager>();
            if (m_ObjectPoolManager == null)
            {
                throw new InvalidOperationException("ObjectPoolManager 无效，请确保 ObjectPoolComponent 先于 UIComponent 初始化。");
            }

            m_InstancePool = m_ObjectPoolManager.CreateSingleGettingObjectPool<UIViewInstanceObject>(
                new ObjectPoolConfig
                {
                    Name = "UIViewInstancesPool",
                    AutoReleaseInterval = m_InstanceAutoReleaseInterval,
                    Capacity = m_InstanceCapacity,
                    ExpireTime = m_InstanceExpireTime,
                    Priority = m_InstancePriority
                });
        }

        /// <summary>
        /// UI 管理器轮询。
        /// </summary>
        public override void Update()
        {
            int count = 0;
            while (m_RecycleQueue.Count > 0 && count < m_DestroyMaxNumPerFrame)
            {
                IUIView uiView = m_RecycleQueue.Dequeue();
                bool inObjectPools = uiView.InObjectPools;
                uiView.OnRecycle();
                if (inObjectPools)
                {
                    m_InstancePool.Put(uiView.Target);
                }
                else
                {
                    ReleaseUIView(uiView.Target);
                }
                count++;
            }

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                uiGroup.Value.Update();
            }
        }

        /// <summary>
        /// 关闭并清理 UI 管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_IsShutdown = true;
            CloseAllLoadingUIViews();
            CloseAllLoadedUIViews();
            m_UIViewIndex.Clear();
            m_UIGroups.Clear();
            m_UIViewsBeingLoaded.Clear();
            m_UIViewsToReleaseOnLoad.Clear();
            m_RecycleQueue.Clear();
            m_InstancePool = null;
            m_ObjectPoolManager = null;
            m_UIViewRegistry.Clear();
            m_CachedUIViewResults.Clear();
            m_CachedUIGroupResults.Clear();
            m_CachedSerialIDResults.Clear();
            m_UnitSettings = null;
            m_SafeAreaProvider = null;
            m_AssetManager = null;
            m_PrefabManager = null;
        }

        /// <summary>
        /// 异步加载并解析 UI 视图注册表。
        /// Phase 1：遍历 m_UnitSettings，对每个有效 unit 并行加载 JSON 数据到局部 dataCache。
        /// Phase 2：通过 ITable<IUIViewRow> 协变直接访问 DataList 填充 m_UIViewRegistry。
        /// </summary>
        /// <returns>是否全部加载成功。</returns>
        public override async UniTask<bool> LoadAsync()
        {
            if (m_UnitSettings == null || m_UnitSettings.Count == 0)
            {
                Log.Debug(LogTag.UI, "UI 注册表 UnitSettings 为空，跳过加载。");
                return true;
            }

            BuildUIRegistryAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);

            LubanDataCache dataCache = new LubanDataCache();
            List<UniTask<bool>> tasks = new List<UniTask<bool>>(m_UnitSettings.Count);
            for (int i = 0; i < m_UnitSettings.Count; i++)
            {
                UIUnitSetting unit = m_UnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }
                tasks.Add(new LubanDataReceiver(dataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation));
            }

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            return BuildUITablesFromCache(dataCache);
        }

        /// <summary>
        /// 同步加载并解析 UI 视图注册表。
        /// Phase 1：遍历 m_UnitSettings，对每个有效 unit 同步加载 JSON 数据到局部 dataCache。
        /// Phase 2：通过 ITable<IUIViewRow> 协变直接访问 DataList 填充 m_UIViewRegistry。
        /// </summary>
        public override void LoadSync()
        {
            if (m_UnitSettings == null || m_UnitSettings.Count == 0)
            {
                Log.Debug(LogTag.UI, "UI 注册表 UnitSettings 为空，跳过加载。");
                return;
            }

            BuildUIRegistrySyncDelegates(out DataReceiver.LoadAssetSyncFunc syncLoadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);

            LubanDataCache dataCache = new LubanDataCache();
            for (int i = 0; i < m_UnitSettings.Count; i++)
            {
                UIUnitSetting unit = m_UnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }

                bool success = new LubanDataReceiver(dataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
                if (!success)
                {
                    Log.Error(LogTag.UI, "同步加载 UI 数据失败，资源地址 '{0}'。", unit.AssetLocation);
                    return;
                }
            }

            if (!BuildUITablesFromCache(dataCache))
            {
                Log.Error(LogTag.UI, "同步加载 UI 注册表失败。");
            }
        }

        /// <summary>
        /// 通过泛型同步打开视图，使用注册表中的默认配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewSync<T>(object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewSync(entry.AssetLocation, entry.UIGroupName, entry.PauseCoveredUIView, entry.InObjectPools, userData);
        }

        /// <summary>
        /// 通过泛型同步打开视图，允许覆盖 PauseCoveredUIView。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewSync<T>(bool pauseCoveredUIView, object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewSync(entry.AssetLocation, entry.UIGroupName, pauseCoveredUIView, entry.InObjectPools, userData);
        }

        /// <summary>
        /// 通过泛型同步打开视图，允许覆盖 PauseCoveredUIView 与 InObjectPools。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewSync<T>(bool pauseCoveredUIView, bool inObjectPools, object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewSync(entry.AssetLocation, entry.UIGroupName, pauseCoveredUIView, inObjectPools, userData);
        }

        /// <summary>
        /// 同步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public override int OpenUIViewSync(string assetLocation, string uiGroupName, bool pauseCoveredUIView, bool inObjectPools, object userData)
        {
            PrepareOpenUIView(assetLocation, uiGroupName, inObjectPools, out int serialID, out UIGroup uiGroup, out string instanceName, out UIViewInstanceObject instanceObject);
            if (instanceObject == null)
            {
                Transform parent = ((MonoBehaviour)uiGroup.Helper).transform;
                object uiViewGO = LoadUIViewSync(assetLocation, parent);
                instanceObject = UIViewInstanceObject.Create(instanceName, uiViewGO);
                if (inObjectPools)
                {
                    m_InstancePool.Register(instanceObject, true);
                }
                InternalOpenUIView(serialID, assetLocation, uiGroup, instanceObject.Target, pauseCoveredUIView, inObjectPools, true, 0f, userData);
            }
            else
            {
                InternalOpenUIView(serialID, assetLocation, uiGroup, instanceObject.Target, pauseCoveredUIView, inObjectPools, false, 0f, userData);
            }

            return serialID;
        }

        /// <summary>
        /// 通过泛型异步打开视图，使用注册表中的默认配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewAsync<T>(object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewAsync(entry.AssetLocation, entry.UIGroupName, entry.PauseCoveredUIView, entry.InObjectPools, userData);
        }

        /// <summary>
        /// 通过泛型异步打开视图，允许覆盖 PauseCoveredUIView。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewAsync<T>(bool pauseCoveredUIView, object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewAsync(entry.AssetLocation, entry.UIGroupName, pauseCoveredUIView, entry.InObjectPools, userData);
        }

        /// <summary>
        /// 通过泛型异步打开视图，允许覆盖 PauseCoveredUIView 与 InObjectPools。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public override int OpenUIViewAsync<T>(bool pauseCoveredUIView, bool inObjectPools, object userData = null)
        {
            IUIViewRow entry = GetUIViewRow<T>();
            if (entry == null)
            {
                return -1;
            }

            return OpenUIViewAsync(entry.AssetLocation, entry.UIGroupName, pauseCoveredUIView, inObjectPools, userData);
        }

        /// <summary>
        /// 异步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public override int OpenUIViewAsync(string assetLocation, string uiGroupName, bool pauseCoveredUIView, bool inObjectPools, object userData)
        {
            PrepareOpenUIView(assetLocation, uiGroupName, inObjectPools, out int serialID, out UIGroup uiGroup, out string instanceName, out UIViewInstanceObject instanceObject);
            if (instanceObject == null)
            {
                m_UIViewsBeingLoaded.Add(serialID, assetLocation);
                Transform parent = ((MonoBehaviour)uiGroup.Helper).transform;
                LoadUIViewAsync(assetLocation, parent, OpenUIViewInfo.Create(serialID, uiGroup, pauseCoveredUIView, inObjectPools, userData)).Forget();
            }
            else
            {
                InternalOpenUIView(serialID, assetLocation, uiGroup, instanceObject.Target, pauseCoveredUIView, inObjectPools, false, 0f, userData);
            }

            return serialID;
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="serialID">要关闭视图的序列编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void CloseUIView(int serialID, object userData)
        {
            if (IsLoadingUIView(serialID))
            {
                m_UIViewsToReleaseOnLoad.Add(serialID);
                m_UIViewsBeingLoaded.Remove(serialID);
                return;
            }

            IUIView uiView = GetUIView(serialID);
            if (uiView == null)
            {
                throw new InvalidOperationException($"找不到序列编号为 '{serialID}' 的视图。");
            }

            CloseUIView(uiView, userData);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="uiView">要关闭的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void CloseUIView(IUIView uiView, object userData)
        {
            if (uiView == null)
            {
                throw new ArgumentNullException(nameof(uiView), "视图无效。");
            }

            UIGroup uiGroup = (UIGroup)uiView.UIGroup;
            if (uiGroup == null)
            {
                throw new InvalidOperationException("视图分组无效。");
            }

            uiGroup.RemoveUIView(uiView);
            m_UIViewIndex.Remove(uiView.SerialID);
            uiView.OnClose(m_IsShutdown, userData);
            uiGroup.Refresh();
            m_RecycleQueue.Enqueue(uiView);
        }

        /// <summary>
        /// 关闭所有已加载的视图。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void CloseAllLoadedUIViews(object userData)
        {
            m_CachedUIViewResults.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                uiGroup.Value.InternalGetAllUIViews(m_CachedUIViewResults);
            }

            for (int i = m_CachedUIViewResults.Count - 1; i >= 0; i--)
            {
                IUIView uiView = m_CachedUIViewResults[i];
                if (!HasUIView(uiView.SerialID))
                {
                    continue;
                }

                CloseUIView(uiView, userData);
            }
        }

        /// <summary>
        /// 关闭所有正在加载的视图。
        /// </summary>
        public override void CloseAllLoadingUIViews()
        {
            foreach (KeyValuePair<int, string> entry in m_UIViewsBeingLoaded)
            {
                m_UIViewsToReleaseOnLoad.Add(entry.Key);
            }

            m_UIViewsBeingLoaded.Clear();
        }

        /// <summary>
        /// 增加视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="uiGroupDepth">视图分组深度。</param>
        /// <param name="uiGroupHelper">视图分组辅助器。</param>
        /// <returns>是否增加视图分组成功。</returns>
        public override bool AddUIGroup(string uiGroupName, int uiGroupDepth, IUIGroupHelper uiGroupHelper)
        {
            if (string.IsNullOrEmpty(uiGroupName))
            {
                throw new ArgumentException("视图分组名称无效。", nameof(uiGroupName));
            }

            if (uiGroupHelper == null)
            {
                throw new ArgumentNullException(nameof(uiGroupHelper), "视图分组辅助器无效。");
            }

            if (HasUIGroup(uiGroupName))
            {
                return false;
            }

            m_UIGroups.Add(uiGroupName, new UIGroup(uiGroupName, uiGroupDepth, m_GroupDepthFactor, m_ViewDepthFactor, uiGroupHelper));
            return true;
        }

        /// <summary>
        /// 获取视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>要获取的视图分组。</returns>
        public override IUIGroup GetUIGroup(string uiGroupName)
        {
            if (string.IsNullOrEmpty(uiGroupName))
            {
                throw new ArgumentException("视图分组名称无效。", nameof(uiGroupName));
            }

            return m_UIGroups.TryGetValue(uiGroupName, out UIGroup uiGroup) ? uiGroup : null;
        }

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <returns>所有视图分组。</returns>
        public override IUIGroup[] GetAllUIGroups()
        {
            m_CachedUIGroupResults.Clear();
            GetAllUIGroups(m_CachedUIGroupResults);
            return m_CachedUIGroupResults.ToArray();
        }

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <param name="results">所有视图分组。</param>
        public override void GetAllUIGroups(List<IUIGroup> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "结果列表无效。");
            }

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                results.Add(uiGroup.Value);
            }
        }

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>要获取的视图。</returns>
        public override IUIView GetUIView(int serialID)
        {
            return m_UIViewIndex.TryGetValue(serialID, out IUIView uiView) ? uiView : null;
        }

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图。</returns>
        public override IUIView GetUIView(string assetLocation)
        {
            if (string.IsNullOrEmpty(assetLocation))
            {
                throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
            }

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                IUIView uiView = uiGroup.Value.GetUIView(assetLocation);
                if (uiView != null)
                {
                    return uiView;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图列表。</returns>
        public override IUIView[] GetUIViews(string assetLocation)
        {
            m_CachedUIViewResults.Clear();
            GetUIViews(assetLocation, m_CachedUIViewResults);
            return m_CachedUIViewResults.ToArray();
        }

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="results">要获取的视图列表。</param>
        public override void GetUIViews(string assetLocation, List<IUIView> results)
        {
            if (string.IsNullOrEmpty(assetLocation))
            {
                throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "结果列表无效。");
            }

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                uiGroup.Value.InternalGetUIViews(assetLocation, results);
            }
        }

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <returns>所有已加载的视图。</returns>
        public override IUIView[] GetAllLoadedUIViews()
        {
            m_CachedUIViewResults.Clear();
            GetAllLoadedUIViews(m_CachedUIViewResults);
            return m_CachedUIViewResults.ToArray();
        }

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <param name="results">所有已加载的视图。</param>
        public override void GetAllLoadedUIViews(List<IUIView> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "结果列表无效。");
            }

            results.Clear();
            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                uiGroup.Value.InternalGetAllUIViews(results);
            }
        }

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <returns>所有正在加载视图的序列编号。</returns>
        public override int[] GetAllLoadingUIViewSerialIDs()
        {
            m_CachedSerialIDResults.Clear();
            GetAllLoadingUIViewSerialIDs(m_CachedSerialIDResults);
            return m_CachedSerialIDResults.ToArray();
        }

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <param name="results">所有正在加载视图的序列编号。</param>
        public override void GetAllLoadingUIViewSerialIDs(List<int> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "结果列表无效。");
            }

            results.Clear();
            foreach (KeyValuePair<int, string> entry in m_UIViewsBeingLoaded)
            {
                results.Add(entry.Key);
            }
        }

        /// <summary>
        /// 是否存在视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>是否存在视图分组。</returns>
        public override bool HasUIGroup(string uiGroupName)
        {
            if (string.IsNullOrEmpty(uiGroupName))
            {
                throw new ArgumentException("视图分组名称无效。", nameof(uiGroupName));
            }

            return m_UIGroups.ContainsKey(uiGroupName);
        }

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否存在视图。</returns>
        public override bool HasUIView(int serialID)
        {
            return m_UIViewIndex.ContainsKey(serialID);
        }

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否存在视图。</returns>
        public override bool HasUIView(string assetLocation)
        {
            if (string.IsNullOrEmpty(assetLocation))
            {
                throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
            }

            foreach (KeyValuePair<string, UIGroup> uiGroup in m_UIGroups)
            {
                if (uiGroup.Value.HasUIView(assetLocation))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否正在加载视图。</returns>
        public override bool IsLoadingUIView(int serialID)
        {
            return m_UIViewsBeingLoaded.ContainsKey(serialID);
        }

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否正在加载视图。</returns>
        public override bool IsLoadingUIView(string assetLocation)
        {
            if (string.IsNullOrEmpty(assetLocation))
            {
                throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
            }

            foreach (string loadingLocation in m_UIViewsBeingLoaded.Values)
            {
                if (loadingLocation == assetLocation)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 是否是合法的视图。
        /// </summary>
        /// <param name="uiView">视图。</param>
        /// <returns>视图是否合法。</returns>
        public override bool IsValidUIView(IUIView uiView)
        {
            if (uiView == null)
            {
                return false;
            }

            return HasUIView(uiView.SerialID);
        }

        /// <summary>
        /// 激活视图。
        /// </summary>
        /// <param name="uiView">要激活的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void RefocusUIView(IUIView uiView, object userData)
        {
            if (uiView == null)
            {
                throw new ArgumentNullException(nameof(uiView), "视图无效。");
            }

            UIGroup uiGroup = (UIGroup)uiView.UIGroup;
            if (uiGroup == null)
            {
                throw new InvalidOperationException("视图分组无效。");
            }

            uiGroup.RefocusUIView(uiView, userData);
            uiGroup.Refresh();
            uiView.OnRefocus(userData);
        }

        /// <summary>
        /// 设置视图目标对象是否被加锁。
        /// </summary>
        /// <param name="uiViewTarget">要设置是否被加锁的视图目标对象。</param>
        /// <param name="locked">视图目标对象是否被加锁。</param>
        public override void SetUIViewTargetLocked(object uiViewTarget, bool locked)
        {
            if (uiViewTarget == null)
            {
                throw new ArgumentNullException(nameof(uiViewTarget), "视图实例无效。");
            }

            m_InstancePool.SetLocked(uiViewTarget, locked);
        }

        /// <summary>
        /// 设置视图目标对象的优先级。
        /// </summary>
        /// <param name="uiViewTarget">要设置优先级的视图目标对象。</param>
        /// <param name="priority">视图目标对象优先级。</param>
        public override void SetUIViewTargetPriority(object uiViewTarget, int priority)
        {
            if (uiViewTarget == null)
            {
                throw new ArgumentNullException(nameof(uiViewTarget), "视图实例无效。");
            }

            m_InstancePool.SetPriority(uiViewTarget, priority);
        }

        /// <summary>
        /// 获取设备安全区域（实际物理像素区域信息）。
        /// </summary>
        /// <param name="isForGUISystem">是否以 GUI 系统坐标系为准。
        /// true：坐标原点在屏幕左上角；false：坐标原点在屏幕左下角。
        /// </param>
        /// <returns>矩形信息（实际物理像素区域，非画布自动缩放后的坐标）。</returns>
        public override Rect GetDeviceSafeArea(bool isForGUISystem = false)
        {
            if (m_SafeAreaProvider == null)
            {
                return new Rect(0, 0, Screen.width, Screen.height);
            }

            SafeAreaData data = m_SafeAreaProvider.GetSafeArea();

            float logicalWidth = data.Right - data.Left;
            float logicalHeight = data.Bottom - data.Top;
            float logicalY = isForGUISystem ? (data.ScreenHeight - logicalHeight - data.Top) : data.Top;

            Rect safeArea = new Rect(
                Mathf.CeilToInt(data.Left * data.PixelRatio),
                Mathf.CeilToInt(logicalY * data.PixelRatio),
                Mathf.CeilToInt(logicalWidth * data.PixelRatio),
                Mathf.CeilToInt(logicalHeight * data.PixelRatio)
            );

            return safeArea;
        }
    }
}
