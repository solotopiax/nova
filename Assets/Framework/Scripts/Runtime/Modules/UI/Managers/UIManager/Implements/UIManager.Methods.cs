/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.Methods.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 私有辅助方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 构造实例池检索名称。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>用于对象池检索的名称。</returns>
        private static string GetInstanceName(string assetLocation)
        {
            return assetLocation;
        }

        /// <summary>
        /// 内部打开视图。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <param name="assetLocation">视图Asset 地址。</param>
        /// <param name="uiGroup">视图分组。</param>
        /// <param name="uiViewTarget">视图目标对象。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="duration">加载持续时间。</param>
        /// <param name="userData">用户自定义数据。</param>
        private void InternalOpenUIView(int serialID, string assetLocation, UIGroup uiGroup, object uiViewTarget, bool pauseCoveredUIView, bool inObjectPools, bool isNewInstance, float duration, object userData)
        {
            try
            {
                IUIView uiView = CreateUIView(uiViewTarget, uiGroup);
                if (uiView == null)
                {
                    throw new InvalidOperationException("视图辅助器创建视图失败。");
                }

                uiView.OnInit(serialID, assetLocation, uiGroup, pauseCoveredUIView, inObjectPools, isNewInstance, userData);
                uiGroup.AddUIView(uiView);
                m_UIViewIndex[serialID] = uiView;
                uiView.OnOpen(userData);
                uiGroup.Refresh();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"打开视图失败，Asset 地址 '{assetLocation}'：{exception.Message}", exception);
            }
        }

        /// <summary>
        /// 异步加载视图成功回调。
        /// </summary>
        /// <param name="assetLocation">视图Asset 地址。</param>
        /// <param name="uiViewTarget">已实例化的视图目标对象。</param>
        /// <param name="duration">加载持续时间。</param>
        /// <param name="openUIViewInfo">打开视图信息。</param>
        private void OnLoadUIViewAsyncSuccess(string assetLocation, object uiViewTarget, float duration, OpenUIViewInfo openUIViewInfo)
        {
            if (m_IsShutdown)
            {
                ReleaseUIView(uiViewTarget);
                ReferencePool.Put(openUIViewInfo);
                return;
            }

            if (m_UIViewsToReleaseOnLoad.Contains(openUIViewInfo.SerialID))
            {
                m_UIViewsToReleaseOnLoad.Remove(openUIViewInfo.SerialID);
                ReleaseUIView(uiViewTarget);
                ReferencePool.Put(openUIViewInfo);
                return;
            }

            m_UIViewsBeingLoaded.Remove(openUIViewInfo.SerialID);
            string instanceName = GetInstanceName(assetLocation);
            UIViewInstanceObject instanceObject = UIViewInstanceObject.Create(instanceName, uiViewTarget);
            if (openUIViewInfo.InObjectPools)
            {
                m_InstancePool.Register(instanceObject, true);
            }

            InternalOpenUIView(openUIViewInfo.SerialID, assetLocation, openUIViewInfo.UIGroup, instanceObject.Target, openUIViewInfo.PauseCoveredUIView, openUIViewInfo.InObjectPools, true, duration, openUIViewInfo.UserData);
            ReferencePool.Put(openUIViewInfo);
        }

        /// <summary>
        /// 异步加载视图失败回调。
        /// </summary>
        /// <param name="assetLocation">视图Asset 地址。</param>
        /// <param name="errorMessage">错误信息。</param>
        /// <param name="openUIViewInfo">打开视图信息。</param>
        private void OnLoadUIViewAsyncFailure(string assetLocation, string errorMessage, OpenUIViewInfo openUIViewInfo)
        {
            if (m_IsShutdown)
            {
                ReferencePool.Put(openUIViewInfo);
                return;
            }

            if (m_UIViewsToReleaseOnLoad.Contains(openUIViewInfo.SerialID))
            {
                m_UIViewsToReleaseOnLoad.Remove(openUIViewInfo.SerialID);
                ReferencePool.Put(openUIViewInfo);
                return;
            }

            m_UIViewsBeingLoaded.Remove(openUIViewInfo.SerialID);
            ReferencePool.Put(openUIViewInfo);
            Log.Error(LogTag.UI, "加载视图失败，Asset 地址 '{0}'：{1}", assetLocation, errorMessage);
        }

        /// <summary>
        /// 同步加载视图资源，通过 AssetComponent 加载并实例化 Prefab。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <param name="parent">实例化后的父节点。</param>
        /// <returns>实例化后的视图对象。</returns>
        private object LoadUIViewSync(string assetLocation, Transform parent)
        {
            GameObject go = m_PrefabManager.InstantiateSync(assetLocation, parent);
            if (go == null)
            {
                throw new InvalidOperationException($"同步加载视图失败，Asset 地址 '{assetLocation}'。");
            }

            return go;
        }

        /// <summary>
        /// 分配下一个可用的视图序列编号，自动跳过已占用的编号。
        /// </summary>
        /// <returns>可用的序列编号。</returns>
        private int AllocateSerialID()
        {
            int attempts = 0;
            do
            {
                m_Serial++;
                if (m_Serial <= 0)
                {
                    m_Serial = 1;
                }

                if (!m_UIViewIndex.ContainsKey(m_Serial))
                {
                    return m_Serial;
                }

                attempts++;
            }
            while (attempts < m_UIViewIndex.Count + 1);

            throw new InvalidOperationException("无法分配视图序列编号，所有编号均已被占用。");
        }

        /// <summary>
        /// 准备打开视图的公共参数：校验输入、分配序列号、查询对象池。
        /// inObjectPools 为 false 时直接跳过对象池查询，强制走新实例加载路径。
        /// </summary>
        /// <param name="assetLocation">视图Asset 地址。</param>
        /// <param name="uiGroupName">视图分组名称。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="serialID">分配的序列编号。</param>
        /// <param name="uiGroup">目标分组。</param>
        /// <param name="instanceName">对象池实例名称。</param>
        /// <param name="instanceObject">对象池实例对象（可能为 null）。</param>
        private void PrepareOpenUIView(string assetLocation, string uiGroupName, bool inObjectPools, out int serialID, out UIGroup uiGroup, out string instanceName, out UIViewInstanceObject instanceObject)
        {
            if (string.IsNullOrEmpty(assetLocation))
            {
                throw new ArgumentException("视图Asset 地址无效。", nameof(assetLocation));
            }

            if (string.IsNullOrEmpty(uiGroupName))
            {
                throw new ArgumentException("视图分组名称无效。", nameof(uiGroupName));
            }

            uiGroup = (UIGroup)GetUIGroup(uiGroupName);
            if (uiGroup == null)
            {
                throw new InvalidOperationException($"视图分组 '{uiGroupName}' 不存在。");
            }

            serialID = AllocateSerialID();
            instanceName = GetInstanceName(assetLocation);
            instanceObject = inObjectPools ? m_InstancePool.Get(instanceName) : null;
        }

        /// <summary>
        /// 异步加载视图资源，通过 AssetComponent 加载并实例化 Prefab。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <param name="parent">实例化后的父节点。</param>
        /// <param name="openUIViewInfo">打开视图信息。</param>
        private async UniTask LoadUIViewAsync(string assetLocation, Transform parent, OpenUIViewInfo openUIViewInfo)
        {
            try
            {
                GameObject go = await m_PrefabManager.InstantiateAsync(assetLocation, parent);

                if (go == null)
                {
                    OnLoadUIViewAsyncFailure(assetLocation, "异步加载返回空实例。", openUIViewInfo);
                    return;
                }

                OnLoadUIViewAsyncSuccess(assetLocation, go, 0f, openUIViewInfo);
            }
            catch (Exception exception)
            {
                OnLoadUIViewAsyncFailure(assetLocation, exception.Message, openUIViewInfo);
            }
        }

        /// <summary>
        /// 通过泛型类型从注册表中查找视图配置条目。
        /// </summary>
        /// <typeparam name="T">UIView 子类类型。</typeparam>
        /// <returns>视图配置数据行，未找到时返回 null 并输出错误日志。</returns>
        private IUIViewRow GetUIViewRow<T>() where T : UIView
        {
            string typeName = typeof(T).Name;
            if (!m_UIViewRegistry.TryGetValue(typeName, out IUIViewRow row))
            {
                Log.Error(LogTag.UI, "未在注册表中找到视图配置：'{0}'，请检查 UI 配置表。", typeName);
                return null;
            }

            return row;
        }

        /// <summary>
        /// Luban Tables 类名称。
        /// </summary>
        private const string c_UITablesClassName = "UITables";

        /// <summary>
        /// 反射构造 UITables 实例，通过 ITable<IUIViewRow> 协变直接访问 DataList 填充 m_UIViewRegistry。
        /// </summary>
        /// <param name="dataCache">Phase 1 写入的数据加载缓存，消费后由本方法清空。</param>
        /// <returns>是否构建成功。</returns>
        private bool BuildUITablesFromCache(LubanDataCache dataCache)
        {
            if (dataCache == null || dataCache.DataMap.Count == 0)
            {
                return true;
            }

            Func<string, JArray> loader = key =>
            {
                if (dataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.UI, "数据缓存中未找到 UI 数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.UI, "IConfigManager 未注册，无法加载 UI 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanTablesLoader.Load(c_UITablesClassName, namespace_, loader);
            if (tables == null)
            {
                return false;
            }

            foreach (var kv in tables)
            {
                FillRegistryFromTable(kv.Value);
            }

            Log.Debug(LogTag.UI, "UI 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", m_UnitSettings.Count, m_UIViewRegistry.Count);
            return true;
        }

        /// <summary>
        /// 从单个 ITable 中提取数据行，通过 ITable<IUIViewRow> 协变填充到 m_UIViewRegistry。
        /// </summary>
        /// <param name="table">Luban 表实例。</param>
        private void FillRegistryFromTable(ITable table)
        {
            if (!(table is ITable<IUIViewRow> typedTable))
            {
                Log.Warning(LogTag.UI, "表类型 '{0}' 未实现 ITable<IUIViewRow>，已跳过。请确认 Luban bean 已实现 IUIViewRow 接口。", table.GetType().Name);
                return;
            }

            IReadOnlyList<IUIViewRow> dataList = typedTable.DataList;
            for (int i = 0; i < dataList.Count; i++)
            {
                IUIViewRow row = dataList[i];
                if (row == null || string.IsNullOrEmpty(row.Name))
                {
                    continue;
                }

                if (m_UIViewRegistry.ContainsKey(row.Name))
                {
                    Log.Warning(LogTag.UI, "UI 注册表存在重复条目：{0}，已跳过。", row.Name);
                    continue;
                }

                m_UIViewRegistry.Add(row.Name, row);
            }
        }

        /// <summary>
        /// 创建视图：将实例化后的 GameObject 挂载到分组节点并获取 UIView 组件。
        /// Prefab 必须预挂业务 UIView 子类组件；HybridCLR 模式下业务 dll 加载完成后由 Unity 反序列化机制挂上。
        /// </summary>
        /// <param name="uiViewTarget">视图目标对象。</param>
        /// <param name="uiGroup">视图所属的视图分组。</param>
        /// <returns>创建完成的视图。</returns>
        private IUIView CreateUIView(object uiViewTarget, IUIGroup uiGroup)
        {
            GameObject go = uiViewTarget as GameObject;
            if (go == null)
            {
                Log.Error(LogTag.UI, "视图实例无效，无法创建视图。");
                return null;
            }

            Transform tf = go.transform;
            // 父 helper 在 Screen Space - Camera 模式下其 world transform 由 Unity 按 PlaneDistance/缩放自动维护；
            // SetParent 默认保留世界坐标，会把视图的 localPosition.z 推成与父级缩放成反比的极端值（飞到摄像机后方），
            // 故显式以 worldPositionStays=false 入参，并把 local 三件套归位到 (0, identity, 1)。
            tf.SetParent(((MonoBehaviour)uiGroup.Helper).transform, false);
            tf.localPosition = Vector3.zero;
            tf.localRotation = Quaternion.identity;
            tf.localScale = Vector3.one;

            UIView uiView = go.GetComponent<UIView>();
            if (uiView == null)
            {
                Log.Error(LogTag.UI, "视图 Prefab '{0}' 上未找到 UIView 组件，请确保 Prefab 已预挂 UIView 子类组件并在构建期 dll 类型可见。", go.name);
                return null;
            }

            return uiView;
        }

        /// <summary>
        /// 释放视图：销毁已实例化的视图 GameObject。
        /// </summary>
        /// <param name="uiViewTarget">要释放的视图目标对象。</param>
        private void ReleaseUIView(object uiViewTarget)
        {
            GameObject go = uiViewTarget as GameObject;
            if (go != null)
            {
                m_PrefabManager.Destroy(go);
            }
        }

        /// <summary>
        /// 构造 UI 注册表加载所需的资源加载委托（异步路径）。
        /// </summary>
        /// <param name="loadFunc">输出的资源异步加载委托。</param>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        private void BuildUIRegistryAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            loadFunc = async (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }

        /// <summary>
        /// 构造 UI 注册表加载所需的资源加载委托（同步路径）。
        /// </summary>
        /// <param name="syncLoadFunc">输出的资源同步加载委托。</param>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        private void BuildUIRegistrySyncDelegates(out DataReceiver.LoadAssetSyncFunc syncLoadFunc, out DataReceiver.ReleaseAssetAction releaseFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = assetManager.LoadSync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }
    }
}
