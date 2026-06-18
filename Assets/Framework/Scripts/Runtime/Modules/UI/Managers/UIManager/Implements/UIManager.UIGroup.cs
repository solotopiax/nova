/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.UIGroup.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 视图分组
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 视图分组。
        /// </summary>
        private sealed partial class UIGroup : IUIGroup
        {
            /// <summary>
            /// 视图分组名称。
            /// </summary>
            private readonly string m_Name;
            public string Name => m_Name;

            /// <summary>
            /// 视图分组深度，影响渲染层级。
            /// </summary>
            private int m_Depth;
            public int Depth
            {
                get => m_Depth;
                set
                {
                    if (m_Depth == value)
                    {
                        return;
                    }

                    m_Depth = value;
                    m_UIGroupHelper.SetDepth(m_Depth);
                    Refresh();
                }
            }

            /// <summary>
            /// 视图分组是否处于暂停状态。
            /// </summary>
            private bool m_Pause;
            public bool Pause
            {
                get => m_Pause;
                set
                {
                    if (m_Pause == value)
                    {
                        return;
                    }

                    m_Pause = value;
                    Refresh();
                }
            }

            /// <summary>
            /// 视图分组辅助器，负责深度设置等平台相关操作。
            /// </summary>
            private readonly IUIGroupHelper m_UIGroupHelper;

            /// <summary>
            /// 视图分组深度换算系数（由 UIComponent Inspector 配置，UIManager 透传）。
            /// </summary>
            private readonly int m_GroupDepthFactor;
            public int GroupDepthFactor => m_GroupDepthFactor;

            /// <summary>
            /// 视图内部深度换算系数（由 UIComponent Inspector 配置，UIManager 透传）。
            /// </summary>
            private readonly int m_ViewDepthFactor;
            public int ViewDepthFactor => m_ViewDepthFactor;

            /// <summary>
            /// 视图信息链表，链表头为当前最顶层视图。
            /// </summary>
            private readonly LinkedList<UIViewInfo> m_UIViewInfos;

            /// <summary>
            /// 视图到链表节点的索引字典，用于 O(1) 查找。
            /// </summary>
            private readonly Dictionary<IUIView, LinkedListNode<UIViewInfo>> m_UIViewInfoMap;

            /// <summary>
            /// 遍历链表时缓存的下一节点，防止视图被移除时迭代失效。
            /// </summary>
            private LinkedListNode<UIViewInfo> m_CachedNode;

            /// <summary>
            /// 缓存的视图查询结果列表，避免查询方法中重复分配。
            /// </summary>
            private readonly List<IUIView> m_CachedUIViewResults;

            /// <summary>
            /// Refresh 遍历时的视图信息快照，防止回调中链表变更导致迭代失效。
            /// </summary>
            private readonly List<UIViewInfo> m_RefreshSnapshot;

            /// <summary>
            /// 获取视图分组中视图数量。
            /// </summary>
            public int UIViewCount => m_UIViewInfos.Count;

            /// <summary>
            /// 获取当前视图。
            /// </summary>
            public IUIView CurrentUIView => m_UIViewInfos.First != null ? m_UIViewInfos.First.Value.UIView : null;

            /// <summary>
            /// 获取视图分组辅助器。
            /// </summary>
            public IUIGroupHelper Helper => m_UIGroupHelper;

            /// <summary>
            /// 初始化视图分组的新实例。
            /// </summary>
            /// <param name="name">视图分组名称。</param>
            /// <param name="depth">视图分组深度。</param>
            /// <param name="groupDepthFactor">视图分组深度换算系数。</param>
            /// <param name="viewDepthFactor">视图内部深度换算系数。</param>
            /// <param name="uiGroupHelper">视图分组辅助器。</param>
            public UIGroup(string name, int depth, int groupDepthFactor, int viewDepthFactor, IUIGroupHelper uiGroupHelper)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("视图分组名称无效。", nameof(name));
                }

                if (uiGroupHelper == null)
                {
                    throw new ArgumentNullException(nameof(uiGroupHelper), "视图分组辅助器无效。");
                }

                m_Name = name;
                m_Pause = false;
                m_GroupDepthFactor = groupDepthFactor;
                m_ViewDepthFactor = viewDepthFactor;
                m_UIGroupHelper = uiGroupHelper;
                m_UIGroupHelper.SetDepthFactor(groupDepthFactor);
                m_UIViewInfos = new LinkedList<UIViewInfo>();
                m_UIViewInfoMap = new Dictionary<IUIView, LinkedListNode<UIViewInfo>>();
                m_CachedNode = null;
                m_CachedUIViewResults = new List<IUIView>();
                m_RefreshSnapshot = new List<UIViewInfo>();
                Depth = depth;
            }  

            /// <summary>
            /// 视图分组轮询。
            /// </summary>
            public void Update()
            {
                LinkedListNode<UIViewInfo> current = m_UIViewInfos.First;
                while (current != null)
                {
                    if (current.Value.Paused)
                    {
                        break;
                    }

                    m_CachedNode = current.Next;
                    current.Value.UIView.OnUpdate();
                    current = m_CachedNode;
                    m_CachedNode = null;
                }
            }

            /// <summary>
            /// 视图分组中是否存在视图。
            /// </summary>
            /// <param name="serialID">视图序列编号。</param>
            /// <returns>视图分组中是否存在视图。</returns>
            public bool HasUIView(int serialID)
            {
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.SerialID == serialID)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 视图分组中是否存在视图。
            /// </summary>
            /// <param name="assetLocation">视图资源地址。</param>
            /// <returns>视图分组中是否存在视图。</returns>
            public bool HasUIView(string assetLocation)
            {
                if (string.IsNullOrEmpty(assetLocation))
                {
                    throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
                }

                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.AssetLocation == assetLocation)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 从视图分组中获取视图。
            /// </summary>
            /// <param name="serialID">视图序列编号。</param>
            /// <returns>要获取的视图。</returns>
            public IUIView GetUIView(int serialID)
            {
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.SerialID == serialID)
                    {
                        return uiViewInfo.UIView;
                    }
                }

                return null;
            }

            /// <summary>
            /// 从视图分组中获取视图。
            /// </summary>
            /// <param name="assetLocation">视图资源地址。</param>
            /// <returns>要获取的视图。</returns>
            public IUIView GetUIView(string assetLocation)
            {
                if (string.IsNullOrEmpty(assetLocation))
                {
                    throw new ArgumentException("视图资源地址无效。", nameof(assetLocation));
                }

                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.AssetLocation == assetLocation)
                    {
                        return uiViewInfo.UIView;
                    }
                }

                return null;
            }

            /// <summary>
            /// 从视图分组中获取视图列表。
            /// </summary>
            /// <param name="assetLocation">视图资源地址。</param>
            /// <returns>要获取的视图列表。</returns>
            public IUIView[] GetUIViews(string assetLocation)
            {
                m_CachedUIViewResults.Clear();
                GetUIViews(assetLocation, m_CachedUIViewResults);
                return m_CachedUIViewResults.ToArray();
            }

            /// <summary>
            /// 从视图分组中获取视图列表。
            /// </summary>
            /// <param name="assetLocation">视图资源地址。</param>
            /// <param name="results">要获取的视图列表。</param>
            public void GetUIViews(string assetLocation, List<IUIView> results)
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
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.AssetLocation == assetLocation)
                    {
                        results.Add(uiViewInfo.UIView);
                    }
                }
            }

            /// <summary>
            /// 从视图分组中获取所有视图。
            /// </summary>
            /// <returns>视图分组中的所有视图。</returns>
            public IUIView[] GetAllUIViews()
            {
                m_CachedUIViewResults.Clear();
                GetAllUIViews(m_CachedUIViewResults);
                return m_CachedUIViewResults.ToArray();
            }

            /// <summary>
            /// 从视图分组中获取所有视图。
            /// </summary>
            /// <param name="results">视图分组中的所有视图。</param>
            public void GetAllUIViews(List<IUIView> results)
            {
                if (results == null)
                {
                    throw new ArgumentNullException(nameof(results), "结果列表无效。");
                }

                results.Clear();
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    results.Add(uiViewInfo.UIView);
                }
            }

            /// <summary>
            /// 往视图分组增加视图。
            /// </summary>
            /// <param name="uiView">要增加的视图。</param>
            public void AddUIView(IUIView uiView)
            {
                LinkedListNode<UIViewInfo> node = m_UIViewInfos.AddFirst(UIViewInfo.Create(uiView));
                m_UIViewInfoMap[uiView] = node;
            }

            /// <summary>
            /// 从视图分组移除视图。
            /// </summary>
            /// <param name="uiView">要移除的视图。</param>
            public void RemoveUIView(IUIView uiView)
            {
                if (!m_UIViewInfoMap.TryGetValue(uiView, out LinkedListNode<UIViewInfo> node))
                {
                    throw new InvalidOperationException($"找不到视图信息，序列编号 '{uiView.SerialID}'，资源地址 '{uiView.AssetLocation}'。");
                }

                UIViewInfo uiViewInfo = node.Value;

                if (!uiViewInfo.Covered)
                {
                    uiViewInfo.Covered = true;
                    uiView.OnCover();
                }

                if (!uiViewInfo.Paused)
                {
                    uiViewInfo.Paused = true;
                    uiView.OnPause();
                }

                if (m_CachedNode == node)
                {
                    m_CachedNode = m_CachedNode.Next;
                }

                m_UIViewInfos.Remove(node);
                m_UIViewInfoMap.Remove(uiView);
                ReferencePool.Put(uiViewInfo);
            }

            /// <summary>
            /// 激活视图。
            /// </summary>
            /// <param name="uiView">要激活的视图。</param>
            /// <param name="userData">用户自定义数据。</param>
            public void RefocusUIView(IUIView uiView, object userData)
            {
                if (!m_UIViewInfoMap.TryGetValue(uiView, out LinkedListNode<UIViewInfo> node))
                {
                    throw new InvalidOperationException("找不到视图信息。");
                }

                m_UIViewInfos.Remove(node);
                m_UIViewInfos.AddFirst(node);
                m_UIViewInfoMap[uiView] = node;
            }

            /// <summary>
            /// 刷新视图分组。
            /// 每次视图增删或深度变化时被调用，从链表头（顶层）向尾部遍历，重新计算并广播所有视图的状态。
            /// 只有头部的视图 cover 为 false，其他视图 cover 都为 true。
            /// 遍历前先做快照，防止回调中修改链表（如 CloseUIView）导致迭代失效。
            /// 回调可能触发递归 Refresh（如 OnResume 中 OpenUIView），递归结束时快照被清空，
            /// 外层通过 m_RefreshSnapshot.Count == 0 检测到递归已正确处理所有视图，立即退出。
            /// </summary>
            public void Refresh()
            {
                // 遍历前建立快照，防止回调修改链表。
                m_RefreshSnapshot.Clear();
                LinkedListNode<UIViewInfo> snapshotNode = m_UIViewInfos.First;
                while (snapshotNode != null)
                {
                    m_RefreshSnapshot.Add(snapshotNode.Value);
                    snapshotNode = snapshotNode.Next;
                }

                bool pause = m_Pause;
                bool cover = false;
                int depth = UIViewCount;

                for (int i = 0; i < m_RefreshSnapshot.Count; i++)
                {
                    UIViewInfo info = m_RefreshSnapshot[i];

                    // 视图在回调中已被移除（UIView 被清空）则跳过。
                    if (info.UIView == null)
                    {
                        continue;
                    }

                    // 视图仍在链表中才处理（通过索引字典确认）。
                    if (!m_UIViewInfoMap.ContainsKey(info.UIView))
                    {
                        continue;
                    }

                    info.UIView.OnDepthChanged(Depth, depth--);

                    // 递归 Refresh 已正确处理所有视图，外层过期的局部状态不应继续覆盖。
                    if (m_RefreshSnapshot.Count == 0)
                    {
                        return;
                    }

                    // 再次确认视图仍存在（OnDepthChanged 回调可能关闭视图）。
                    if (info.UIView == null || !m_UIViewInfoMap.ContainsKey(info.UIView))
                    {
                        continue;
                    }

                    if (pause)
                    {
                        if (!info.Covered)
                        {
                            info.Covered = true;
                            info.UIView.OnCover();
                            if (m_RefreshSnapshot.Count == 0)
                            {
                                return;
                            }

                            if (info.UIView == null || !m_UIViewInfoMap.ContainsKey(info.UIView))
                            {
                                continue;
                            }
                        }

                        if (!info.Paused)
                        {
                            info.Paused = true;
                            info.UIView.OnPause();
                            if (m_RefreshSnapshot.Count == 0)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (info.Paused)
                        {
                            info.Paused = false;
                            info.UIView.OnResume();
                            if (m_RefreshSnapshot.Count == 0)
                            {
                                return;
                            }

                            if (info.UIView == null || !m_UIViewInfoMap.ContainsKey(info.UIView))
                            {
                                continue;
                            }
                        }

                        // 一个向下传播的开关，如果当前视图标记了这个值，则说明从当前 uiView 开始，下面所有 uiView 都要暂停。
                        if (info.UIView.PauseCoveredUIView)
                        {
                            // 将暂停状态向下传播。
                            pause = true;
                        }

                        // 说明已经不是最顶层的视图了，需要标记为被遮挡。
                        if (cover)
                        {
                            if (!info.Covered)
                            {
                                info.Covered = true;
                                info.UIView.OnCover();
                                if (m_RefreshSnapshot.Count == 0)
                                {
                                    return;
                                }
                            }
                        }
                        else  // 说明是最顶层的视图，需要标记为未被遮挡。
                        {
                            if (info.Covered)
                            {
                                info.Covered = false;
                                info.UIView.OnReveal();
                                if (m_RefreshSnapshot.Count == 0)
                                {
                                    return;
                                }
                            }

                            // 将遮挡状态向下传播。
                            cover = true;
                        }
                    }
                }

                m_RefreshSnapshot.Clear();
            }

            /// <summary>
            /// 从视图分组中收集指定资源的视图，追加至结果列表（不清空原有内容）。
            /// </summary>
            /// <param name="assetLocation">视图资源地址。</param>
            /// <param name="results">用于接收结果的视图列表。</param>
            internal void InternalGetUIViews(string assetLocation, List<IUIView> results)
            {
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    if (uiViewInfo.UIView.AssetLocation == assetLocation)
                    {
                        results.Add(uiViewInfo.UIView);
                    }
                }
            }

            /// <summary>
            /// 将视图分组中所有视图追加至结果列表（不清空原有内容）。
            /// </summary>
            /// <param name="results">用于接收结果的视图列表。</param>
            internal void InternalGetAllUIViews(List<IUIView> results)
            {
                foreach (UIViewInfo uiViewInfo in m_UIViewInfos)
                {
                    results.Add(uiViewInfo.UIView);
                }
            }

            /// <summary>
            /// 获取视图对应的视图信息。
            /// </summary>
            /// <param name="uiView">目标视图。</param>
            /// <returns>视图信息，若不存在则返回 null。</returns>
            private UIViewInfo GetUIViewInfo(IUIView uiView)
            {
                if (uiView == null)
                {
                    throw new ArgumentNullException(nameof(uiView), "视图无效。");
                }

                return m_UIViewInfoMap.TryGetValue(uiView, out LinkedListNode<UIViewInfo> node) ? node.Value : null;
            }
        }
    }
}
