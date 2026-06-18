/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponent.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 组件
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class UIComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_UIManager = Util.TypeCreator.Create<IUIManager>(m_CurUIManagerTypeName);
            if (m_UIManager == null)
            {
                throw new InvalidOperationException("UIManager 无效。");
            }

            if (m_InstanceRoot == null)
            {
                GameObject go = new GameObject("UIViewInstancesRoot");
                go.AddComponent<Canvas>();
                go.AddComponent<CanvasScaler>();
                go.AddComponent<GraphicRaycaster>();
                m_InstanceRoot = go.transform;
                m_InstanceRoot.SetParent(gameObject.transform);
                m_InstanceRoot.localScale = Vector3.one;
            }

            m_InstanceRoot.gameObject.layer = LayerMask.NameToLayer("UI");

            ApplyInstanceRootCanvasScaler();
        }

        /// <summary>
        /// 销毁。
        /// </summary>
        private void OnDestroy()
        {
            m_LoadTcs = null;
            IsLoadOver = false;
            m_UIManager = null;
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            m_UIManager.Initialize(new UIManagerConfig
            {
                InstanceAutoReleaseInterval = m_InstanceAutoReleaseInterval,
                InstanceCapacity = m_InstanceCapacity,
                InstanceExpireTime = m_InstanceExpireTime,
                InstancePriority = m_InstancePriority,
                DestroyMaxNumPerFrame = m_DestroyMaxNumPerFrame,
                GroupDepthFactor = m_GroupDepthFactor,
                ViewDepthFactor = m_ViewDepthFactor,
                UnitSettings = m_UISettings?.UIUnitsSettings,
            });

            for (int i = 0; i < m_UIGroups.Length; i++)
            {
                if (!AddUIGroup(m_UIGroups[i].Name, m_UIGroups[i].Depth))
                {
                    Log.Warning(LogTag.UI, "增加视图分组 '{0}' 失败。", m_UIGroups[i].Name);
                }
            }

            m_UIManager.CreateInstancePool();
        }

        /// <summary>
        /// 同步加载 UI 视图注册表。
        /// </summary>
        public void LoadSync()
        {
            m_UIManager.LoadSync();
            IsLoadOver = true;
        }

        /// <summary>
        /// 异步加载 UI 视图注册表。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public async UniTask<bool> LoadAsync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            if (m_LoadTcs != null)
            {
                return await m_LoadTcs.Task;
            }

            m_LoadTcs = new UniTaskCompletionSource<bool>();
            UniTaskCompletionSource<bool> tcs = m_LoadTcs;

            bool success;

            try
            {
                success = await m_UIManager.LoadAsync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.UI, "UIComponent.LoadAsync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            tcs.TrySetResult(success);
            m_LoadTcs = null;

            return success;
        }

        /// <summary>
        /// 将屏幕设计分辨率与宽高适配阀值应用到实例根节点对应的 CanvasScaler 上。
        /// 实例根自身或其父节点上的 Canvas 的 CanvasScaler 会被查找并赋值；若未找到则不修改。
        /// </summary>
        private void ApplyInstanceRootCanvasScaler()
        {
            CanvasScaler scaler = null;
            Canvas canvas = m_InstanceRoot.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                scaler = canvas.GetComponent<CanvasScaler>();
            }

            if (scaler == null)
            {
                scaler = m_InstanceRoot.GetComponent<CanvasScaler>();
            }

            if (scaler != null)
            {
                scaler.referenceResolution = m_ScreenDesignedResolution;
                scaler.matchWidthOrHeight = m_ScreenWidthHeightMatchValue;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                m_InstanceRootCanvasScaler = scaler;
            }
        }

        /// <summary>
        /// 通过泛型同步打开视图，使用注册表中的默认配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public int OpenUIViewSync<T>(object userData = null) where T : UIView
        {
            return m_UIManager.OpenUIViewSync<T>(userData);
        }

        /// <summary>
        /// 通过泛型同步打开视图，允许覆盖 PauseCoveredUIView。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public int OpenUIViewSync<T>(bool pauseCoveredUIView, object userData = null) where T : UIView
        {
            return m_UIManager.OpenUIViewSync<T>(pauseCoveredUIView, userData);
        }

        /// <summary>
        /// 同步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewSync(string assetLocation, string uiGroupName)
        {
            return m_UIManager.OpenUIViewSync(assetLocation, uiGroupName);
        }

        /// <summary>
        /// 同步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewSync(string assetLocation, string uiGroupName, bool pauseCoveredUIView)
        {
            return m_UIManager.OpenUIViewSync(assetLocation, uiGroupName, pauseCoveredUIView);
        }

        /// <summary>
        /// 同步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewSync(string assetLocation, string uiGroupName, object userData)
        {
            return m_UIManager.OpenUIViewSync(assetLocation, uiGroupName, userData);
        }

        /// <summary>
        /// 同步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewSync(string assetLocation, string uiGroupName, bool pauseCoveredUIView, object userData)
        {
            return m_UIManager.OpenUIViewSync(assetLocation, uiGroupName, pauseCoveredUIView, userData);
        }

        /// <summary>
        /// 通过泛型异步打开视图，使用注册表中的默认配置。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public int OpenUIViewAsync<T>(object userData = null) where T : UIView
        {
            return m_UIManager.OpenUIViewAsync<T>(userData);
        }

        /// <summary>
        /// 通过泛型异步打开视图，允许覆盖 PauseCoveredUIView。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号，失败返回 -1。</returns>
        public int OpenUIViewAsync<T>(bool pauseCoveredUIView, object userData = null) where T : UIView
        {
            return m_UIManager.OpenUIViewAsync<T>(pauseCoveredUIView, userData);
        }

        /// <summary>
        /// 异步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewAsync(string assetLocation, string uiGroupName)
        {
            return m_UIManager.OpenUIViewAsync(assetLocation, uiGroupName);
        }

        /// <summary>
        /// 异步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewAsync(string assetLocation, string uiGroupName, bool pauseCoveredUIView)
        {
            return m_UIManager.OpenUIViewAsync(assetLocation, uiGroupName, pauseCoveredUIView);
        }

        /// <summary>
        /// 异步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewAsync(string assetLocation, string uiGroupName, object userData)
        {
            return m_UIManager.OpenUIViewAsync(assetLocation, uiGroupName, userData);
        }

        /// <summary>
        /// 异步打开视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>视图的序列编号。</returns>
        public int OpenUIViewAsync(string assetLocation, string uiGroupName, bool pauseCoveredUIView, object userData)
        {
            return m_UIManager.OpenUIViewAsync(assetLocation, uiGroupName, pauseCoveredUIView, userData);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="serialID">要关闭视图的序列编号。</param>
        public void CloseUIView(int serialID)
        {
            m_UIManager.CloseUIView(serialID);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="serialID">要关闭视图的序列编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void CloseUIView(int serialID, object userData)
        {
            m_UIManager.CloseUIView(serialID, userData);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="uiView">要关闭的视图。</param>
        public void CloseUIView(UIView uiView)
        {
            m_UIManager.CloseUIView(uiView);
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        /// <param name="uiView">要关闭的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void CloseUIView(UIView uiView, object userData)
        {
            m_UIManager.CloseUIView(uiView, userData);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="serialIDs">要关闭视图的序列编号列表。</param>
        public void CloseUIViews(int[] serialIDs)
        {
            m_UIManager.CloseUIViews(serialIDs);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="serialIDs">要关闭视图的序列编号列表。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void CloseUIViews(int[] serialIDs, object userData)
        {
            m_UIManager.CloseUIViews(serialIDs, userData);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="uiViews">要关闭的视图列表。</param>
        public void CloseUIViews(UIView[] uiViews)
        {
            m_UIManager.CloseUIViews(uiViews);
        }

        /// <summary>
        /// 关闭视图列表。
        /// </summary>
        /// <param name="uiViews">要关闭的视图列表。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void CloseUIViews(UIView[] uiViews, object userData)
        {
            m_UIManager.CloseUIViews(uiViews, userData);
        }

        /// <summary>
        /// 关闭所有已加载的视图。
        /// </summary>
        public void CloseAllLoadedUIViews()
        {
            m_UIManager.CloseAllLoadedUIViews();
        }

        /// <summary>
        /// 关闭所有已加载的视图。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void CloseAllLoadedUIViews(object userData)
        {
            m_UIManager.CloseAllLoadedUIViews(userData);
        }

        /// <summary>
        /// 关闭所有正在加载的视图。
        /// </summary>
        public void CloseAllLoadingUIViews()
        {
            m_UIManager.CloseAllLoadingUIViews();
        }

        /// <summary>
        /// 增加视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>是否增加视图分组成功。</returns>
        public bool AddUIGroup(string uiGroupName)
        {
            return AddUIGroup(uiGroupName, 0);
        }

        /// <summary>
        /// 增加视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="depth">视图分组深度。</param>
        /// <returns>是否增加视图分组成功。</returns>
        public bool AddUIGroup(string uiGroupName, int depth)
        {
            if (m_UIManager.HasUIGroup(uiGroupName))
            {
                return false;
            }

            IUIGroupHelper uiGroupHelper = Util.TypeCreator.Create<IUIGroupHelper>(m_CurUIGroupHelperTypeName);
            if (uiGroupHelper == null)
            {
                throw new InvalidOperationException("UIGroupHelper 无效。");
            }

            MonoBehaviour helperMono = (MonoBehaviour)uiGroupHelper;
            helperMono.name = Txt.Format("UIGroup - {0}", uiGroupName);
            helperMono.gameObject.layer = LayerMask.NameToLayer("UI");
            // 父 Canvas 在 Screen Space - Camera 模式下其 world transform 由 Unity 按 PlaneDistance/缩放自动维护；
            // SetParent 默认保留世界坐标，会把 localPosition.z 推成与 PlaneDistance/父级缩放成反比的极端值，
            // 因此显式以 worldPositionStays=false 入参，让 helper 子节点的 local 三件套保持 (0,identity,1)。
            helperMono.transform.SetParent(m_InstanceRoot, false);
            helperMono.transform.localPosition = Vector3.zero;
            helperMono.transform.localRotation = Quaternion.identity;
            helperMono.transform.localScale = Vector3.one;

            return m_UIManager.AddUIGroup(uiGroupName, depth, uiGroupHelper);
        }

        /// <summary>
        /// 获取视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>要获取的视图分组。</returns>
        public IUIGroup GetUIGroup(string uiGroupName)
        {
            return m_UIManager.GetUIGroup(uiGroupName);
        }

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <returns>所有视图分组。</returns>
        public IUIGroup[] GetAllUIGroups()
        {
            return m_UIManager.GetAllUIGroups();
        }

        /// <summary>
        /// 获取所有视图分组。
        /// </summary>
        /// <param name="results">所有视图分组。</param>
        public void GetAllUIGroups(List<IUIGroup> results)
        {
            m_UIManager.GetAllUIGroups(results);
        }

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>要获取的视图。</returns>
        public UIView GetUIView(int serialID)
        {
            return (UIView)m_UIManager.GetUIView(serialID);
        }

        /// <summary>
        /// 获取视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图。</returns>
        public UIView GetUIView(string assetLocation)
        {
            return (UIView)m_UIManager.GetUIView(assetLocation);
        }

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>要获取的视图列表。</returns>
        public UIView[] GetUIViews(string assetLocation)
        {
            m_InternalUIViewResults.Clear();
            m_UIManager.GetUIViews(assetLocation, m_InternalUIViewResults);
            UIView[] result = new UIView[m_InternalUIViewResults.Count];
            for (int i = 0; i < m_InternalUIViewResults.Count; i++)
            {
                result[i] = (UIView)m_InternalUIViewResults[i];
            }
            return result;
        }

        /// <summary>
        /// 获取视图列表。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="results">要获取的视图列表。</param>
        public void GetUIViews(string assetLocation, List<UIView> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            m_UIManager.GetUIViews(assetLocation, m_InternalUIViewResults);
            foreach (IUIView uiView in m_InternalUIViewResults)
            {
                results.Add((UIView)uiView);
            }
        }

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <returns>所有已加载的视图。</returns>
        public UIView[] GetAllLoadedUIViews()
        {
            m_InternalUIViewResults.Clear();
            m_UIManager.GetAllLoadedUIViews(m_InternalUIViewResults);
            UIView[] result = new UIView[m_InternalUIViewResults.Count];
            for (int i = 0; i < m_InternalUIViewResults.Count; i++)
            {
                result[i] = (UIView)m_InternalUIViewResults[i];
            }
            return result;
        }

        /// <summary>
        /// 获取所有已加载的视图。
        /// </summary>
        /// <param name="results">所有已加载的视图。</param>
        public void GetAllLoadedUIViews(List<UIView> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            m_UIManager.GetAllLoadedUIViews(m_InternalUIViewResults);
            foreach (IUIView uiView in m_InternalUIViewResults)
            {
                results.Add((UIView)uiView);
            }
        }

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <returns>所有正在加载视图的序列编号。</returns>
        public int[] GetAllLoadingUIViewSerialIDs()
        {
            return m_UIManager.GetAllLoadingUIViewSerialIDs();
        }

        /// <summary>
        /// 获取所有正在加载视图的序列编号。
        /// </summary>
        /// <param name="results">所有正在加载视图的序列编号。</param>
        public void GetAllLoadingUIViewSerialIDs(List<int> results)
        {
            m_UIManager.GetAllLoadingUIViewSerialIDs(results);
        }

        /// <summary>
        /// 是否存在视图分组。
        /// </summary>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <returns>是否存在视图分组。</returns>
        public bool HasUIGroup(string uiGroupName)
        {
            return m_UIManager.HasUIGroup(uiGroupName);
        }

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否存在视图。</returns>
        public bool HasUIView(int serialID)
        {
            return m_UIManager.HasUIView(serialID);
        }

        /// <summary>
        /// 是否存在视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否存在视图。</returns>
        public bool HasUIView(string assetLocation)
        {
            return m_UIManager.HasUIView(assetLocation);
        }

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <returns>是否正在加载视图。</returns>
        public bool IsLoadingUIView(int serialID)
        {
            return m_UIManager.IsLoadingUIView(serialID);
        }

        /// <summary>
        /// 是否正在加载视图。
        /// </summary>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <returns>是否正在加载视图。</returns>
        public bool IsLoadingUIView(string assetLocation)
        {
            return m_UIManager.IsLoadingUIView(assetLocation);
        }

        /// <summary>
        /// 是否是合法的视图。
        /// </summary>
        /// <param name="uiView">视图。</param>
        /// <returns>视图是否合法。</returns>
        public bool IsValidUIView(UIView uiView)
        {
            return m_UIManager.IsValidUIView(uiView);
        }

        /// <summary>
        /// 激活视图。
        /// </summary>
        /// <param name="uiView">要激活的视图。</param>
        public void RefocusUIView(UIView uiView)
        {
            m_UIManager.RefocusUIView(uiView);
        }

        /// <summary>
        /// 激活视图。
        /// </summary>
        /// <param name="uiView">要激活的视图。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void RefocusUIView(UIView uiView, object userData)
        {
            m_UIManager.RefocusUIView(uiView, userData);
        }

        /// <summary>
        /// 设置视图实例是否被加锁。
        /// </summary>
        /// <param name="uiView">要设置是否被加锁的视图。</param>
        /// <param name="locked">视图实例是否被加锁。</param>
        public void SetUIViewTargetLocked(UIView uiView, bool locked)
        {
            if (uiView == null)
            {
                Log.Warning(LogTag.UI, "视图无效。");
                return;
            }

            m_UIManager.SetUIViewTargetLocked(uiView.Target, locked);
        }

        /// <summary>
        /// 设置视图实例的优先级。
        /// </summary>
        /// <param name="uiView">要设置优先级的视图。</param>
        /// <param name="priority">视图实例优先级。</param>
        public void SetUIViewTargetPriority(UIView uiView, int priority)
        {
            if (uiView == null)
            {
                Log.Warning(LogTag.UI, "视图无效。");
                return;
            }

            m_UIManager.SetUIViewTargetPriority(uiView.Target, priority);
        }

        /// <summary>
        /// 获取设备安全区域（实际物理像素区域信息）。
        /// </summary>
        /// <param name="isForGUISystem">是否以 GUI 系统坐标系为准。
        /// true：坐标原点在屏幕左上角；false：坐标原点在屏幕左下角。
        /// </param>
        /// <returns>矩形信息（实际物理像素区域，非画布自动缩放后的坐标）。</returns>
        public Rect GetDeviceSafeArea(bool isForGUISystem = false)
        {
            return m_UIManager.GetDeviceSafeArea(isForGUISystem);
        }
        
    }
}
