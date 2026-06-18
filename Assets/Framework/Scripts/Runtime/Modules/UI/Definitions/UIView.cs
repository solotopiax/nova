/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIView.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图基类 - 公共方法与生命周期接口
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图基类。
    /// 继承此类实现具体视图逻辑，通过重写生命周期方法响应视图状态变化。
    /// </summary>
    public abstract partial class UIView : MonoBehaviour, IUIView
    {
        /// <summary>
        /// 框架初始化入口（实现 IUIView.OnInit）。
        /// 负责维护框架侧字段，仅在新实例时触发。
        /// <see cref="OnInit(object)"/> 钩子。
        /// </summary>
        /// <param name="serialID">视图序列编号。</param>
        /// <param name="assetLocation">视图资源地址。</param>
        /// <param name="uiGroup">视图所属的视图分组。</param>
        /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
        /// <param name="inObjectPools">是否启用对象池缓存。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnInit(int serialID, string assetLocation, IUIGroup uiGroup, bool pauseCoveredUIView, bool inObjectPools, bool isNewInstance, object userData)
        {
            m_SerialID = serialID;
            m_AssetLocation = assetLocation;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIView = pauseCoveredUIView;
            m_InObjectPools = inObjectPools;

            if (!isNewInstance)
            {
                return;
            }

            try
            {
                OnInit(userData);
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.UI, "视图 '[{0}]{1}' OnInit 异常：{2}", m_SerialID, m_AssetLocation, exception);
            }
        }

        /// <summary>
        /// 视图回收。
        /// 视图被回收进对象池时触发。
        /// 基类实现负责重置框架侧字段，注意：子类重写时应调用 base.OnRecycle()。
        /// </summary>
        public virtual void OnRecycle()
        {
            m_SerialID = 0;
            m_UIGroup = null;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIView = true;
            m_InObjectPools = true;
            m_Available = false;
            m_Visible = false;
        }

        /// <summary>
        /// 视图打开。
        /// 默认实现仅设置可用 / 可见状态。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnOpen(object userData)
        {
            m_Available = true;
            Visible = true;
        }

        /// <summary>
        /// 视图关闭。
        /// 默认实现还原 Layer 并设置不可用 / 不可见状态。
        /// </summary>
        /// <param name="isShutdown">是否是关闭视图管理器时触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnClose(bool isShutdown, object userData)
        {
            InternalSetLayerRecursively(gameObject, m_OriginalLayer);
            Visible = false;
            m_Available = false;
        }

        /// <summary>
        /// 视图暂停。
        /// 默认实现隐藏视图。
        /// </summary>
        public virtual void OnPause()
        {
            Visible = false;
        }

        /// <summary>
        /// 视图暂停恢复。
        /// 默认实现仅恢复可见状态。
        /// </summary>
        public virtual void OnResume()
        {
            Visible = true;
        }

        /// <summary>
        /// 视图遮挡。
        /// 被上层视图遮挡时触发。
        /// </summary>
        public virtual void OnCover()
        {
        }

        /// <summary>
        /// 视图遮挡恢复。
        /// 上层视图关闭后触发。
        /// </summary>
        public virtual void OnReveal()
        {
        }

        /// <summary>
        /// 视图激活。
        /// 视图被重新激活（Refocus）时触发。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnRefocus(object userData)
        {
        }

        /// <summary>
        /// 视图轮询。
        /// 每帧由框架驱动调用。
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>
        /// 视图深度改变。
        /// 默认实现通过增量来更新所有子 Canvas 的 sortingOrder。
        /// 注意：子类重写时应调用 base.OnDepthChanged(...) 以保持 DepthInUIGroup 同步。
        /// </summary>
        /// <param name="uiGroupDepth">视图分组深度。</param>
        /// <param name="depthInUIGroup">视图在视图分组中的深度。</param>
        public virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            m_DepthInUIGroup = depthInUIGroup;

            int oldDepth = Depth;
            int targetDepth = m_UIGroup.GroupDepthFactor * uiGroupDepth + m_UIGroup.ViewDepthFactor * depthInUIGroup;
            // 相当于 deltaDepth = targetDepth - 上一次已施加的累积偏移量。 
            // 这一项的作用是撤销历史修改，回到原始基准，使得无论 OnDepthChanged 被调用多少次，每次都从原始值出发重新施加目标偏移，保证绝对位置始终正确。
            int deltaDepth = targetDepth - (oldDepth - OriginalDepth);
            // 遍历所有子 Canvas 统一加同一个 delta，保留了 View 内部子 Canvas 的相对层级关系。
            GetComponentsInChildren(true, m_CachedCanvasContainer);
            for (int i = 0; i < m_CachedCanvasContainer.Count; i++)
            {
                m_CachedCanvasContainer[i].sortingOrder += deltaDepth;
            }

            m_CachedCanvasContainer.Clear();
        }

        /// <summary>
        /// 关闭视图。
        /// </summary>
        public void Close()
        {
            Nova.UI.CloseUIView(this);
        }

        /// <summary>
        /// 播放 UI 音效。
        /// </summary>
        /// <param name="uiSoundID">音效 ID。</param>
        public void PlayUISound(int uiSoundID)
        {
            // by taoye: 这里需要播放 UI 音效，暂时先注释掉。
            // GameEntry.Sound.PlayUISound(uiSoundID);
        }

        /// <summary>
        /// 设置全局主字体。
        /// 初始化时应用到该视图下所有 Text 组件。
        /// </summary>
        /// <param name="mainFont">主字体。</param>
        public static void SetMainFont(Font mainFont)
        {
            // by taoye: 这里需要制定全新的字体适配机制，需要重构！

            if (mainFont == null)
            {
                Log.Warning(LogTag.UI, "主字体无效。");
                return;
            }

            s_MainFont = mainFont;
        }

    }
}
