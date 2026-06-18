/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNavTreeView.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树导航主 View — 公共方法与生命周期
 *            职责：将 DemoTreeData 静态树渲染为可滚动、可折叠的 UGUI 树形列表；
 *            接入 Nova.UI SafeArea；叶子点击仅打印日志占位。
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航主 View，继承框架 UIView。
    /// 首次打开时从 DemoTreeData.Root 深度遍历实例化节点视图；
    /// 每次打开时应用 SafeArea 并刷新初始可见性（仅根级子节点可见）。
    /// </summary>
    public sealed partial class DemoNavTreeView : UIView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 深度遍历 DemoTreeData.Root，按节点深度实例化节点项到 Content 下，并订阅点击事件。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            m_NodeViews.Clear();
            BuildNodeViews(DemoTreeData.Root, depth: 0);
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 执行顺序：base -> ApplySafeArea -> RefreshVisibility -> 强制重建布局。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ApplySafeArea();
            RefreshVisibility();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
        }

        /// <summary>
        /// 视图关闭钩子。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 视图轮询钩子，每帧由 UI 框架驱动调用。
        /// 用于推动背景颜色在 HSV 色环上沿 Hue 通道匀速循环。
        /// </summary>
        public override void OnUpdate()
        {
            base.OnUpdate();

            TickBackgroundHue();
        }
    }
}
