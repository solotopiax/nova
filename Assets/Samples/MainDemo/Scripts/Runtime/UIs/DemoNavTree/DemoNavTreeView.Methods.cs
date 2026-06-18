/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNavTreeView.Methods.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树导航主 View — 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航主 View，继承框架 UIView。
    /// </summary>
    public sealed partial class DemoNavTreeView : UIView
    {
        /// <summary>
        /// 深度优先遍历节点树，逐节点实例化视图并绑定数据。
        /// 根节点本身不实例化视图，只遍历其子节点作为一级入口。
        /// </summary>
        /// <param name="node">当前遍历节点。</param>
        /// <param name="depth">当前节点的树深度（根节点子节点深度为 0）。</param>
        private void BuildNodeViews(DemoNode node, int depth)
        {
            if (node.Children == null)
            {
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                DemoNode child = node.Children[i];

                DemoNavTreeNodeView nodeView = Instantiate(m_NodeItemPrefab, m_Content);
                nodeView.Bind(child, depth, OnNodeClicked);
                m_NodeViews.Add(nodeView);

                BuildNodeViews(child, depth + 1);
            }
        }

        /// <summary>
        /// 应用设备 SafeArea 到 m_RootSafeArea 的 offsetMin / offsetMax。
        /// 通过 Nova.UI.GetDeviceSafeArea 获取物理像素矩形并换算为 Canvas 坐标边距。
        /// </summary>
        private void ApplySafeArea()
        {
            Rect safeRect = Nova.UI.GetDeviceSafeArea(isForGUISystem: false);
            float screenW = Screen.width;
            float screenH = Screen.height;

            float left = safeRect.xMin;
            float right = screenW - safeRect.xMax;
            float bottom = safeRect.yMin;
            float top = screenH - safeRect.yMax;

            m_RootSafeArea.offsetMin = new Vector2(left, bottom);
            m_RootSafeArea.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 节点点击回调。
        /// 分支节点：切换 IsExpanded -> RefreshVisibility -> 强制重建布局。
        /// 叶子节点：打印 Debug 日志占位。
        /// </summary>
        /// <param name="node">被点击的数据节点。</param>
        private void OnNodeClicked(DemoNode node)
        {
            if (node.IsLeaf)
            {
                Log.Debug(LogTag.UI, "[Demo] click leaf: {0}", node.Path);
                node.OpenCallback?.Invoke();
                return;
            }

            node.IsExpanded = !node.IsExpanded;

            for (int i = 0; i < m_NodeViews.Count; i++)
            {
                if (m_NodeViews[i].Node == node)
                {
                    m_NodeViews[i].RefreshFoldIcon();
                    break;
                }
            }

            RefreshVisibility();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_Content);
        }

        /// <summary>
        /// 推动背景 Hue 相位匀速前进，并把 HSV(Hue, m_BackgroundSaturation, m_BackgroundValue) 写回背景 Image。
        /// "大箭头"沿色环循环（Hue 累加），"小箭头"位置（饱和度 / 明度）固定不动。
        /// </summary>
        private void TickBackgroundHue()
        {
            if (m_BackgroundImage == null || m_BackgroundHueCycleSeconds <= 0f)
            {
                return;
            }

            m_BackgroundHuePhase += Time.deltaTime / m_BackgroundHueCycleSeconds;
            if (m_BackgroundHuePhase >= 1f)
            {
                m_BackgroundHuePhase -= Mathf.Floor(m_BackgroundHuePhase);
            }

            Color rgb = Color.HSVToRGB(m_BackgroundHuePhase, m_BackgroundSaturation, m_BackgroundValue);
            rgb.a = m_BackgroundAlpha;
            m_BackgroundImage.color = rgb;
        }

        /// <summary>
        /// 根据祖先链 IsExpanded 状态刷新所有节点视图的 SetActive。
        /// 根级子节点（Parent == Root）始终可见；更深的节点仅在所有祖先均展开时可见。
        /// </summary>
        private void RefreshVisibility()
        {
            DemoNode root = DemoTreeData.Root;

            for (int i = 0; i < m_NodeViews.Count; i++)
            {
                DemoNode n = m_NodeViews[i].Node;
                bool active = true;

                DemoNode p = n.Parent;
                while (p != null && p != root)
                {
                    if (!p.IsExpanded)
                    {
                        active = false;
                        break;
                    }

                    p = p.Parent;
                }

                m_NodeViews[i].gameObject.SetActive(active);
            }
        }
    }
}
